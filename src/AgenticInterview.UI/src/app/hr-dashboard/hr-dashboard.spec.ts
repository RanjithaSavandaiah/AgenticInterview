import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { Subject } from 'rxjs';
import { HrDashboard } from './hr-dashboard';
import { InterviewStore } from '../state/interview.store';
import { SignalrService } from '../signalr';

describe('HrDashboard', () => {
  let component: HrDashboard;
  let fixture: ComponentFixture<HrDashboard>;
  let store: InstanceType<typeof InterviewStore>;
  let mockSignalR: {
    startConnection: ReturnType<typeof vi.fn>;
    joinSession: ReturnType<typeof vi.fn>;
    sendUpdate: ReturnType<typeof vi.fn>;
    interviewUpdates$: Subject<{ sessionId: string; message: string }>;
  };

  beforeEach(async () => {
    mockSignalR = {
      startConnection: vi.fn().mockResolvedValue(undefined),
      joinSession: vi.fn(),
      sendUpdate: vi.fn(),
      interviewUpdates$: new Subject()
    };

    await TestBed.configureTestingModule({
      imports: [HrDashboard],
      providers: [
        provideRouter([]),
        provideHttpClient(withFetch()),
        { provide: SignalrService, useValue: mockSignalR }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(HrDashboard);
    component = fixture.componentInstance;
    store = TestBed.inject(InterviewStore);
  });

  // --- Component Creation ---

  it('should create the component', () => {
    expect(component).toBeTruthy();
  });

  // --- Lifecycle ---

  describe('ngOnInit', () => {
    it('should start SignalR session on init', async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      expect(mockSignalR.startConnection).toHaveBeenCalledWith('/hrhub');
    });
  });

  // --- Template Rendering ---

  describe('template rendering', () => {
    it('should display HR Dashboard header', async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      
      const logo = fixture.nativeElement.querySelector('.logo');
      expect(logo?.textContent).toContain('HR Dashboard');
    });

    it('should show candidate name from store', async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      
      const el = fixture.nativeElement as HTMLElement;
      expect(el.textContent).toContain('Awaiting Candidate...');
    });

    it('should show 0 Active Sessions when not in progress', async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      
      const status = fixture.nativeElement.querySelector('.status');
      expect(status?.textContent).toContain('0 Active Sessions');
    });

    it('should show 1 Active Session when InProgress', async () => {
      store.setStatus('InProgress');
      fixture.detectChanges();
      await fixture.whenStable();
      
      const status = fixture.nativeElement.querySelector('.status');
      expect(status?.textContent).toContain('1 Active Session');
    });

    it('should show "No messages yet" when no messages', async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      
      const transcript = fixture.nativeElement.querySelector('.transcript');
      expect(transcript?.textContent).toContain('No messages yet.');
    });

    it('should render messages in the transcript', async () => {
      store.addMessage({
        sourceAgent: 'Technical Interviewer',
        type: 'SystemMessage',
        content: 'Hello candidate!',
        timestamp: new Date().toISOString()
      });
      fixture.detectChanges();
      await fixture.whenStable();
      
      const transcript = fixture.nativeElement.querySelector('.transcript');
      expect(transcript?.textContent).toContain('Technical Interviewer');
      expect(transcript?.textContent).toContain('Hello candidate!');
    });

    it('should display current score', async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      
      const scoreEl = fixture.nativeElement.querySelector('.metrics');
      expect(scoreEl?.textContent).toContain('0/100');
    });

    it('should display proctoring flags count', async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      
      const warningMetric = fixture.nativeElement.querySelector('.metric.warning');
      expect(warningMetric?.textContent).toContain('0 Flags');
    });

    it('should display confidence metric', async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      
      const el = fixture.nativeElement as HTMLElement;
      expect(el.textContent).toContain('Calculating...');
    });

    it('should display AI assessment notes', async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      
      const assessment = fixture.nativeElement.querySelector('.assessment');
      expect(assessment?.textContent).toContain('Waiting for sufficient data');
    });

    it('should have a terminate interview button', async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      
      const terminateBtn = fixture.nativeElement.querySelector('.terminate-btn');
      expect(terminateBtn).toBeTruthy();
      expect(terminateBtn?.textContent).toContain('Terminate Interview');
    });

    it('should display code view section', async () => {
      store.setCandidateCode('int x = 42;');
      fixture.detectChanges();
      await fixture.whenStable();
      
      const codeView = fixture.nativeElement.querySelector('.code-view');
      expect(codeView?.textContent).toContain('int x = 42;');
    });

    it('should show recording dot when InProgress', async () => {
      store.setStatus('InProgress');
      fixture.detectChanges();
      await fixture.whenStable();
      
      const dot = fixture.nativeElement.querySelector('.recording-dot');
      expect(dot).toBeTruthy();
    });
  });
});
