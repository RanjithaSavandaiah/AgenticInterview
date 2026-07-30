import { signalStore, withState, withMethods, patchState, withHooks } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { tap, pipe, switchMap } from 'rxjs';
import { SignalrService } from '../signalr';

export type AgentMessageType = 'TechnicalQuestion' | 'BehavioralQuestion' | 'CodeEvaluation' | 'ProctorWarning' | 'SystemMessage';

export interface InterviewMessage {
  id?: string;
  sourceAgent: string;
  type: AgentMessageType;
  content: string;
  timestamp: string;
}

export interface InterviewState {
  sessionId: string | null;
  status: string;
  candidateName: string;
  currentScore: number;
  proctoringFlags: number;
  confidence: string;
  assessmentNotes: string;
  messages: InterviewMessage[];
  currentQuestion: string;
  candidateCode: string;
  isConnecting: boolean;
  isAgentSpeaking: boolean;
}

const initialState: InterviewState = {
  sessionId: null,
  status: 'NotStarted',
  candidateName: 'Awaiting Candidate...',
  currentScore: 0,
  proctoringFlags: 0,
  confidence: 'Calculating...',
  assessmentNotes: 'Waiting for sufficient data to generate AI assessment...',
  messages: [],
  currentQuestion: '',
  candidateCode: '',
  isConnecting: false,
  isAgentSpeaking: false
};

export const InterviewStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods((store, signalRService = inject(SignalrService), http = inject(HttpClient)) => ({
    
    async startSession(hubUrl: string, sessionId?: string): Promise<void> {
      patchState(store, { isConnecting: true });
      await signalRService.startConnection(hubUrl);
      patchState(store, { isConnecting: false });
      if (sessionId) {
        signalRService.joinSession(sessionId);
      }
    },

    fetchSessionStatus: rxMethod<string>(
      pipe(
        switchMap((sessionId) => 
          http.get<any>(`/api/interview/${sessionId}/status`).pipe(
            tap((response) => {
              patchState(store, {
                sessionId: response.sessionId,
                status: response.status,
                candidateName: response.candidateName,
                currentScore: response.currentScore,
                proctoringFlags: response.proctoringStrikeCount
              });
            })
          )
        )
      )
    ),
    
    fetchMessages: rxMethod<string>(
      pipe(
        switchMap((sessionId) => 
          http.get<any[]>(`/api/interview/${sessionId}/messages`).pipe(
            tap((messages) => {
              messages.forEach(m => {
                patchState(store, (state) => ({
                  messages: [...state.messages, {
                    sourceAgent: m.sourceAgent || 'System',
                    type: 'SystemMessage',
                    content: m.content,
                    timestamp: m.timestamp || new Date().toISOString()
                  } as InterviewMessage]
                }));
              });
            })
          )
        )
      )
    ),
    
    setCandidateCode(code: string): void {
      patchState(store, { candidateCode: code });
    },

    setAgentSpeaking(isSpeaking: boolean): void {
      patchState(store, { isAgentSpeaking: isSpeaking });
    },

    submitCode(sessionId: string, code: string): void {
      patchState(store, { candidateCode: code });
      signalRService.sendUpdate(sessionId, `[CODE_SUBMIT] ${code}`);
    },

    submitAnswer(sessionId: string, answer: string): void {
      signalRService.sendUpdate(sessionId, `[TRANSCRIPT] ${answer}`);
    },
    
    reportMalpractice(sessionId: string, type: string): void {
      signalRService.sendUpdate(sessionId, `[MALPRACTICE] ${type}`);
    },

    addMessage(message: InterviewMessage): void {
      patchState(store, (state) => ({ messages: [...state.messages, message] }));
    },
    
    setStatus(status: string): void {
      patchState(store, { status });
    },
    
    setCurrentQuestion(question: string): void {
      patchState(store, { currentQuestion: question });
    }
  })),
  withHooks({
    onInit(store, signalRService = inject(SignalrService)) {
      signalRService.interviewUpdates$.subscribe(update => {
        if (!update) return;
        
        // Let's parse the string message from backend if it's JSON, 
        // or just add it as a string
        try {
          // If backend sends structured JSON for agent messages
          const parsed = JSON.parse(update.message);
          if (parsed.type === 'QuestionChanged') {
            store.setCurrentQuestion(parsed.question);
          } else if (parsed.type === 'StatusChanged') {
            store.setStatus(parsed.status);
          } else {
             store.addMessage({
              sourceAgent: parsed.sourceAgent || 'System',
              type: parsed.messageType || 'SystemMessage',
              content: parsed.content || update.message,
              timestamp: new Date().toISOString()
            });
          }
        } catch {
          // Fallback if not JSON
          store.addMessage({
            sourceAgent: 'System',
            type: 'SystemMessage',
            content: update.message,
            timestamp: new Date().toISOString()
          });
        }
      });
    }
  })
);
