import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { importProvidersFrom } from '@angular/core';
import { RouterTestingHarness } from '@angular/router/testing';
import { Subject } from 'rxjs';
import { MonacoEditorModule } from 'ngx-monaco-editor-v2';
import { routes } from './app.routes';
import { SignalrService } from './signalr';

describe('App Routes', () => {
  let mockSignalR: {
    startConnection: ReturnType<typeof vi.fn>;
    joinSession: ReturnType<typeof vi.fn>;
    sendUpdate: ReturnType<typeof vi.fn>;
    interviewUpdates$: Subject<{ sessionId: string; message: string }>;
  };

  beforeEach(() => {
    // Mock Web Speech APIs not available in jsdom
    if (!globalThis.SpeechSynthesisUtterance) {
      (globalThis as any).SpeechSynthesisUtterance = class {
        text = ''; rate = 1; pitch = 1; voice = null;
        onstart: (() => void) | null = null;
        onend: (() => void) | null = null;
        onerror: ((e: any) => void) | null = null;
        constructor(text?: string) { this.text = text ?? ''; }
      };
    }
    if (!window.speechSynthesis) {
      (window as any).speechSynthesis = {
        speak: vi.fn((u: any) => { u.onstart?.(); u.onend?.(); }),
        getVoices: () => []
      };
    }

    mockSignalR = {
      startConnection: vi.fn().mockResolvedValue(undefined),
      joinSession: vi.fn(),
      sendUpdate: vi.fn(),
      interviewUpdates$: new Subject()
    };

    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        provideHttpClient(withFetch()),
        importProvidersFrom(MonacoEditorModule.forRoot()),
        { provide: SignalrService, useValue: mockSignalR }
      ]
    });
  });

  it('should define routes for all 3 pages', () => {
    expect(routes.length).toBe(4); // setup, interview, hr-dashboard, wildcard
  });

  it('should have a default route to SetupInterview', () => {
    const defaultRoute = routes.find(r => r.path === '');
    expect(defaultRoute).toBeTruthy();
  });

  it('should have an interview route with sessionId param', () => {
    const interviewRoute = routes.find(r => r.path === 'interview/:sessionId');
    expect(interviewRoute).toBeTruthy();
  });

  it('should have an hr-dashboard route', () => {
    const hrRoute = routes.find(r => r.path === 'hr-dashboard');
    expect(hrRoute).toBeTruthy();
  });

  it('should have a wildcard redirect to default', () => {
    const wildcard = routes.find(r => r.path === '**');
    expect(wildcard).toBeTruthy();
    expect(wildcard?.redirectTo).toBe('');
  });

  it('should navigate to setup page by default', async () => {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/');
    expect(component).toBeTruthy();
  });

  it('should navigate to interview room with sessionId', async () => {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/interview/test-session-123');
    expect(component).toBeTruthy();
  });

  it('should navigate to HR dashboard', async () => {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/hr-dashboard');
    expect(component).toBeTruthy();
  });

  it('should redirect unknown paths to setup', async () => {
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/nonexistent-page');
    expect(router.url).toBe('/');
  });
});
