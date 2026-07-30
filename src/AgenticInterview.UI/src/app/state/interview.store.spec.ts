import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Subject } from 'rxjs';
import { InterviewStore, InterviewMessage } from './interview.store';
import { SignalrService } from '../signalr';

describe('InterviewStore', () => {
  let store: InstanceType<typeof InterviewStore>;
  let mockSignalR: {
    startConnection: ReturnType<typeof vi.fn>;
    joinSession: ReturnType<typeof vi.fn>;
    sendUpdate: ReturnType<typeof vi.fn>;
    interviewUpdates$: Subject<{ sessionId: string; message: string }>;
  };
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    mockSignalR = {
      startConnection: vi.fn().mockResolvedValue(undefined),
      joinSession: vi.fn(),
      sendUpdate: vi.fn(),
      interviewUpdates$: new Subject()
    };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withFetch()),
        provideHttpClientTesting(),
        { provide: SignalrService, useValue: mockSignalR }
      ]
    });

    store = TestBed.inject(InterviewStore);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify(); // Ensure no unexpected HTTP calls
  });

  // --- Initial State ---

  describe('initial state', () => {
    it('should start with NotStarted status', () => {
      expect(store.status()).toBe('NotStarted');
    });

    it('should start with empty messages', () => {
      expect(store.messages()).toEqual([]);
    });

    it('should start with null sessionId', () => {
      expect(store.sessionId()).toBeNull();
    });

    it('should start with isConnecting = false', () => {
      expect(store.isConnecting()).toBe(false);
    });

    it('should start with isAgentSpeaking = false', () => {
      expect(store.isAgentSpeaking()).toBe(false);
    });

    it('should have default candidate name', () => {
      expect(store.candidateName()).toBe('Awaiting Candidate...');
    });

    it('should have default empty candidate code', () => {
      expect(store.candidateCode()).toBe('');
    });

    it('should have 0 proctoring flags', () => {
      expect(store.proctoringFlags()).toBe(0);
    });
  });

  // --- Session Management ---

  describe('startSession', () => {
    it('should set isConnecting during connection', async () => {
      const promise = store.startSession('/hrhub', 'test-session');
      expect(store.isConnecting()).toBe(true);
      await promise;
      expect(store.isConnecting()).toBe(false);
    });

    it('should call SignalR startConnection with correct URL', async () => {
      await store.startSession('/hrhub');
      expect(mockSignalR.startConnection).toHaveBeenCalledWith('/hrhub');
    });

    it('should join session when sessionId is provided', async () => {
      await store.startSession('/hrhub', 'session-123');
      expect(mockSignalR.joinSession).toHaveBeenCalledWith('session-123');
    });

    it('should NOT join session when no sessionId is provided', async () => {
      await store.startSession('/hrhub');
      expect(mockSignalR.joinSession).not.toHaveBeenCalled();
    });
  });

  // --- State Mutations ---

  describe('setCandidateCode', () => {
    it('should update candidate code', () => {
      store.setCandidateCode('console.log("hello")');
      expect(store.candidateCode()).toBe('console.log("hello")');
    });
  });

  describe('setAgentSpeaking', () => {
    it('should set agent speaking to true', () => {
      store.setAgentSpeaking(true);
      expect(store.isAgentSpeaking()).toBe(true);
    });

    it('should set agent speaking to false', () => {
      store.setAgentSpeaking(true);
      store.setAgentSpeaking(false);
      expect(store.isAgentSpeaking()).toBe(false);
    });
  });

  describe('setStatus', () => {
    it('should update interview status', () => {
      store.setStatus('InProgress');
      expect(store.status()).toBe('InProgress');
    });
  });

  describe('setCurrentQuestion', () => {
    it('should update current question', () => {
      store.setCurrentQuestion('What is dependency injection?');
      expect(store.currentQuestion()).toBe('What is dependency injection?');
    });
  });

  describe('addMessage', () => {
    it('should add a message to the list', () => {
      const msg: InterviewMessage = {
        sourceAgent: 'System',
        type: 'SystemMessage',
        content: 'Interview started.',
        timestamp: new Date().toISOString()
      };
      store.addMessage(msg);
      expect(store.messages().length).toBe(1);
      expect(store.messages()[0].content).toBe('Interview started.');
    });

    it('should preserve existing messages when adding new ones', () => {
      store.addMessage({ sourceAgent: 'A', type: 'SystemMessage', content: 'First', timestamp: '' });
      store.addMessage({ sourceAgent: 'B', type: 'SystemMessage', content: 'Second', timestamp: '' });
      expect(store.messages().length).toBe(2);
      expect(store.messages()[0].content).toBe('First');
      expect(store.messages()[1].content).toBe('Second');
    });
  });

  // --- SignalR Communication ---

  describe('submitAnswer', () => {
    it('should send transcript via SignalR', () => {
      store.submitAnswer('session-1', 'My answer is SOLID principles...');
      expect(mockSignalR.sendUpdate).toHaveBeenCalledWith('session-1', '[TRANSCRIPT] My answer is SOLID principles...');
    });
  });

  describe('submitCode', () => {
    it('should send code via SignalR', () => {
      store.submitCode('session-1', 'public class Foo {}');
      expect(mockSignalR.sendUpdate).toHaveBeenCalledWith('session-1', '[CODE_SUBMIT] public class Foo {}');
    });

    it('should update local candidateCode state', () => {
      store.submitCode('session-1', 'int x = 42;');
      expect(store.candidateCode()).toBe('int x = 42;');
    });
  });

  describe('reportMalpractice', () => {
    it('should send malpractice event via SignalR', () => {
      store.reportMalpractice('session-1', 'TAB_SWITCH');
      expect(mockSignalR.sendUpdate).toHaveBeenCalledWith('session-1', '[MALPRACTICE] TAB_SWITCH');
    });

    it('should send COPY_ATTEMPT type', () => {
      store.reportMalpractice('session-1', 'COPY_ATTEMPT');
      expect(mockSignalR.sendUpdate).toHaveBeenCalledWith('session-1', '[MALPRACTICE] COPY_ATTEMPT');
    });

    it('should send PASTE_ATTEMPT type', () => {
      store.reportMalpractice('session-1', 'PASTE_ATTEMPT');
      expect(mockSignalR.sendUpdate).toHaveBeenCalledWith('session-1', '[MALPRACTICE] PASTE_ATTEMPT');
    });

    it('should send WINDOW_BLUR type', () => {
      store.reportMalpractice('session-1', 'WINDOW_BLUR');
      expect(mockSignalR.sendUpdate).toHaveBeenCalledWith('session-1', '[MALPRACTICE] WINDOW_BLUR');
    });
  });

  // --- HTTP Fetching ---

  describe('fetchSessionStatus', () => {
    it('should update store with session status from API', () => {
      store.fetchSessionStatus('session-abc');
      
      const req = httpTesting.expectOne('/api/interview/session-abc/status');
      expect(req.request.method).toBe('GET');
      
      req.flush({
        sessionId: 'session-abc',
        status: 'InProgress',
        candidateName: 'John Doe',
        currentScore: 75,
        proctoringStrikeCount: 1
      });

      expect(store.sessionId()).toBe('session-abc');
      expect(store.status()).toBe('InProgress');
      expect(store.candidateName()).toBe('John Doe');
      expect(store.currentScore()).toBe(75);
      expect(store.proctoringFlags()).toBe(1);
    });
  });

  describe('fetchMessages', () => {
    it('should populate messages from API response', () => {
      store.fetchMessages('session-abc');
      
      const req = httpTesting.expectOne('/api/interview/session-abc/messages');
      expect(req.request.method).toBe('GET');
      
      req.flush([
        { sourceAgent: 'Technical Interviewer', content: 'Hello, welcome!' },
        { sourceAgent: 'Candidate', content: 'Thank you!', timestamp: '2026-07-24T00:00:00Z' }
      ]);

      expect(store.messages().length).toBe(2);
      expect(store.messages()[0].sourceAgent).toBe('Technical Interviewer');
      expect(store.messages()[1].sourceAgent).toBe('Candidate');
      expect(store.messages()[1].timestamp).toBe('2026-07-24T00:00:00Z');
    });

    it('should default sourceAgent to System when missing', () => {
      store.fetchMessages('session-abc');
      
      const req = httpTesting.expectOne('/api/interview/session-abc/messages');
      req.flush([{ content: 'Unknown source message' }]);

      expect(store.messages()[0].sourceAgent).toBe('System');
    });
  });

  // --- SignalR Real-time Updates ---

  describe('SignalR interviewUpdates$ hook', () => {
    it('should parse JSON message and add to messages', () => {
      mockSignalR.interviewUpdates$.next({
        sessionId: 'session-1',
        message: JSON.stringify({
          sourceAgent: 'Technical Interviewer',
          messageType: 'TechnicalQuestion',
          content: 'Explain async/await in C#.'
        })
      });

      expect(store.messages().length).toBe(1);
      expect(store.messages()[0].sourceAgent).toBe('Technical Interviewer');
      expect(store.messages()[0].content).toBe('Explain async/await in C#.');
    });

    it('should handle QuestionChanged event type', () => {
      mockSignalR.interviewUpdates$.next({
        sessionId: 'session-1',
        message: JSON.stringify({ type: 'QuestionChanged', question: 'What is CQRS?' })
      });

      expect(store.currentQuestion()).toBe('What is CQRS?');
    });

    it('should handle StatusChanged event type', () => {
      mockSignalR.interviewUpdates$.next({
        sessionId: 'session-1',
        message: JSON.stringify({ type: 'StatusChanged', status: 'Terminated' })
      });

      expect(store.status()).toBe('Terminated');
    });

    it('should fallback to plain text when message is not JSON', () => {
      mockSignalR.interviewUpdates$.next({
        sessionId: 'session-1',
        message: 'This is a plain text message from the server.'
      });

      expect(store.messages().length).toBe(1);
      expect(store.messages()[0].sourceAgent).toBe('System');
      expect(store.messages()[0].content).toBe('This is a plain text message from the server.');
    });

    it('should ignore null updates', () => {
      // The hook checks for falsy updates and returns early
      mockSignalR.interviewUpdates$.next(null as any);
      expect(store.messages().length).toBe(0);
    });
  });
});
