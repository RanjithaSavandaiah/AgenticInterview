import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { SignalrService } from './signalr';

describe('SignalrService', () => {
  let service: SignalrService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [SignalrService]
    });
    service = TestBed.inject(SignalrService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should have an interviewUpdates$ subject', () => {
    expect(service.interviewUpdates$).toBeDefined();
    expect(service.interviewUpdates$).toBeInstanceOf(Subject);
  });

  it('should not throw when sendUpdate is called without a connection', () => {
    // sendUpdate checks hubConnection state — should be a no-op when not connected
    expect(() => service.sendUpdate('session-1', 'test message')).not.toThrow();
  });

  it('should not throw when joinSession is called without a connection', () => {
    expect(() => service.joinSession('session-1')).not.toThrow();
  });
});
