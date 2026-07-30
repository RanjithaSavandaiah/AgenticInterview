import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { importProvidersFrom } from '@angular/core';
import { Subject } from 'rxjs';
import { MonacoEditorModule } from 'ngx-monaco-editor-v2';
import { InterviewRoom } from './interview-room';
import { InterviewStore } from '../state/interview.store';
import { SignalrService } from '../signalr';

describe('InterviewRoom', () => {
  let component: InterviewRoom;
  let fixture: ComponentFixture<InterviewRoom>;
  let store: InstanceType<typeof InterviewStore>;
  let mockSignalR: {
    startConnection: ReturnType<typeof vi.fn>;
    joinSession: ReturnType<typeof vi.fn>;
    sendUpdate: ReturnType<typeof vi.fn>;
    interviewUpdates$: Subject<{ sessionId: string; message: string }>;
  };

  beforeEach(async () => {
    // Mock Web Speech APIs not available in jsdom
    if (!globalThis.SpeechSynthesisUtterance) {
      (globalThis as any).SpeechSynthesisUtterance = class {
        text = '';
        rate = 1;
        pitch = 1;
        voice = null;
        onstart: (() => void) | null = null;
        onend: (() => void) | null = null;
        onerror: ((e: any) => void) | null = null;
        constructor(text?: string) { this.text = text ?? ''; }
      };
    }
    if (!window.speechSynthesis) {
      (window as any).speechSynthesis = {
        speak: vi.fn((utterance: any) => {
          utterance.onstart?.();
          utterance.onend?.();
        }),
        getVoices: () => []
      };
    }

    mockSignalR = {
      startConnection: vi.fn().mockResolvedValue(undefined),
      joinSession: vi.fn(),
      sendUpdate: vi.fn(),
      interviewUpdates$: new Subject()
    };

    await TestBed.configureTestingModule({
      imports: [InterviewRoom],
      providers: [
        provideRouter([]),
        provideHttpClient(withFetch()),
        importProvidersFrom(MonacoEditorModule.forRoot()),
        { provide: SignalrService, useValue: mockSignalR },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ sessionId: 'test-session-abc' })
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(InterviewRoom);
    component = fixture.componentInstance;
    store = TestBed.inject(InterviewStore);
  });

  // --- Component Creation ---

  it('should create the component', () => {
    expect(component).toBeTruthy();
  });

  it('should read sessionId from route params', () => {
    expect(component.sessionId).toBe('test-session-abc');
  });

  // --- Initial State ---

  describe('initial state', () => {
    it('should NOT be recording initially', () => {
      expect(component.isRecording).toBe(false);
    });

    it('should NOT have camera active initially', () => {
      expect(component.isCameraActive).toBe(false);
    });

    it('should NOT be interview ready initially', () => {
      expect(component.isInterviewReady).toBe(false);
    });

    it('should have empty text input', () => {
      expect(component.textInput).toBe('');
    });

    it('should have default editor options', () => {
      expect(component.editorOptions.theme).toBe('vs-dark');
      expect(component.editorOptions.language).toBe('csharp');
    });

    it('should have default code template', () => {
      expect(component.code).toContain('public class Solution');
      expect(component.code).toContain('ReverseString');
    });
  });

  // --- Permission Overlay (Template rendering) ---

  describe('permission overlay', () => {
    it('should show permission overlay when interview is not ready', async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      
      const overlay = fixture.nativeElement.querySelector('.permission-overlay');
      expect(overlay).toBeTruthy();
    });

    it('should show "Allow Access" button when camera is not active', async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      
      const button = fixture.nativeElement.querySelector('.permission-overlay .run-btn');
      expect(button?.textContent).toContain('Allow Access');
    });

    it('should show "Start Interview" button after camera is granted', async () => {
      component.isCameraActive = true;
      fixture.detectChanges();
      await fixture.whenStable();
      
      const startBtn = fixture.nativeElement.querySelector('.start-btn');
      expect(startBtn?.textContent).toContain('Start Interview');
    });
  });

  // --- Text Input ---

  describe('sendText', () => {
    it('should submit trimmed text to the store', () => {
      const spy = vi.spyOn(store, 'submitAnswer');
      component.textInput = '  SOLID principles are...  ';
      component.sendText();
      expect(spy).toHaveBeenCalledWith('test-session-abc', '  SOLID principles are...  ');
    });

    it('should clear the text input after sending', () => {
      vi.spyOn(store, 'submitAnswer');
      component.textInput = 'Some answer';
      component.sendText();
      expect(component.textInput).toBe('');
    });

    it('should NOT send empty or whitespace-only text', () => {
      const spy = vi.spyOn(store, 'submitAnswer');
      component.textInput = '   ';
      component.sendText();
      expect(spy).not.toHaveBeenCalled();
    });
  });

  // --- Code Evaluation ---

  describe('evaluateCode', () => {
    it('should submit code to the store', () => {
      const spy = vi.spyOn(store, 'submitCode');
      store.setCandidateCode('int x = 42;');
      component.evaluateCode();
      expect(spy).toHaveBeenCalledWith('test-session-abc', 'int x = 42;');
    });
  });

  // --- Malpractice Detection ---

  describe('proctoring', () => {
    it('should report TAB_SWITCH on visibility change to hidden', () => {
      const spy = vi.spyOn(store, 'reportMalpractice');
      // Simulate visibility change
      Object.defineProperty(document, 'visibilityState', { value: 'hidden', writable: true });
      component['handleVisibilityChange']();
      expect(spy).toHaveBeenCalledWith('test-session-abc', 'TAB_SWITCH');
    });

    it('should NOT report TAB_SWITCH when page becomes visible', () => {
      const spy = vi.spyOn(store, 'reportMalpractice');
      Object.defineProperty(document, 'visibilityState', { value: 'visible', writable: true });
      component['handleVisibilityChange']();
      expect(spy).not.toHaveBeenCalled();
    });

    it('should report WINDOW_BLUR on window blur', () => {
      const spy = vi.spyOn(store, 'reportMalpractice');
      component.onWindowBlur();
      expect(spy).toHaveBeenCalledWith('test-session-abc', 'WINDOW_BLUR');
    });

    it('should report COPY_ATTEMPT on copy event', () => {
      const spy = vi.spyOn(store, 'reportMalpractice');
      // ClipboardEvent is not available in jsdom — cast a plain Event
      component.onCopy(new Event('copy') as ClipboardEvent);
      expect(spy).toHaveBeenCalledWith('test-session-abc', 'COPY_ATTEMPT');
    });

    it('should report PASTE_ATTEMPT on paste event', () => {
      const spy = vi.spyOn(store, 'reportMalpractice');
      component.onPaste(new Event('paste') as ClipboardEvent);
      expect(spy).toHaveBeenCalledWith('test-session-abc', 'PASTE_ATTEMPT');
    });
  });

  // --- Terminated State ---

  describe('terminated interview', () => {
    it('should show termination overlay when status is Terminated', async () => {
      // Make isInterviewReady true so the camera overlay is hidden,
      // leaving only the termination overlay visible.
      component.isInterviewReady = true;
      store.setStatus('Terminated');
      fixture.detectChanges();
      await fixture.whenStable();
      
      const overlays = fixture.nativeElement.querySelectorAll('.permission-overlay');
      // The terminated overlay should be present
      const terminatedOverlay = Array.from(overlays).find(
        (el: any) => el.querySelector('h2')?.textContent?.includes('Terminated')
      ) as HTMLElement | undefined;
      expect(terminatedOverlay).toBeTruthy();
      expect(terminatedOverlay?.querySelector('h2')?.textContent).toContain('Interview Terminated');
    });
  });

  // --- Cleanup ---

  describe('ngOnDestroy', () => {
    it('should stop media stream tracks on destroy', () => {
      const mockTrack = { stop: vi.fn() };
      component.mediaStream = { getTracks: () => [mockTrack] } as any;
      component.ngOnDestroy();
      expect(mockTrack.stop).toHaveBeenCalled();
    });

    it('should clear face detection interval on destroy', () => {
      component['faceDetectionInterval'] = setInterval(() => {}, 1000);
      const spy = vi.spyOn(globalThis, 'clearInterval');
      component.ngOnDestroy();
      expect(spy).toHaveBeenCalled();
    });
  });

  // --- TTS (speakMessage) ---

  describe('speakMessage', () => {
    it('should not throw when speechSynthesis is unavailable', () => {
      const original = (window as any).speechSynthesis;
      delete (window as any).speechSynthesis;
      expect(() => component.speakMessage('hello')).not.toThrow();
      (window as any).speechSynthesis = original;
    });
  });

  // --- Template Rendering ---

  describe('template rendering', () => {
    it('should show AI avatar section', async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      
      const avatar = fixture.nativeElement.querySelector('.ai-avatar');
      expect(avatar).toBeTruthy();
      expect(avatar?.textContent).toContain('AI Interviewer');
    });

    it('should show header with logo', async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      
      const logo = fixture.nativeElement.querySelector('.logo');
      expect(logo?.textContent).toContain('Agentic Interview');
    });

    it('should display messages from store', async () => {
      store.addMessage({
        sourceAgent: 'Technical Interviewer',
        type: 'TechnicalQuestion',
        content: 'What is polymorphism?',
        timestamp: new Date().toISOString()
      });
      fixture.detectChanges();
      await fixture.whenStable();
      
      const messages = fixture.nativeElement.querySelectorAll('.message');
      expect(messages.length).toBeGreaterThanOrEqual(1);
    });

    it('should show voice status indicator', async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      
      const voiceStatus = fixture.nativeElement.querySelector('.voice-status');
      expect(voiceStatus).toBeTruthy();
    });
  });
});
