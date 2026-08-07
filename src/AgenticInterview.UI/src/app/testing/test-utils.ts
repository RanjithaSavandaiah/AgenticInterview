import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { SignalrService } from '../signalr';
import { Subject } from 'rxjs';
import { vi } from 'vitest';

// Create a mock SignalR service that all tests can reuse
export function createMockSignalrService(): any {

  const updates$ = new Subject<{ sessionId: string; message: string }>();
  const mock = {
    startConnection: vi.fn().mockResolvedValue(undefined),
    joinSession: vi.fn(),
    sendUpdate: vi.fn(),
    interviewUpdates$: updates$
  };
  return mock as any;
}

// Shared test providers
export function provideTestingModule() {
  return [
    provideRouter([]),
    provideHttpClient(withFetch()),
    { provide: SignalrService, useFactory: createMockSignalrService }
  ];
}
