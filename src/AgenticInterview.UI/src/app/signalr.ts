import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class SignalrService {
  private hubConnection!: signalR.HubConnection;
  
  public interviewUpdates$ = new Subject<{sessionId: string, message: string}>();

  constructor() {}

  public startConnection(url: string): Promise<void> {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(url)
      .withAutomaticReconnect()
      .build();

    this.addReceiveUpdateListener();
    return this.hubConnection.start()
      .then(() => console.log('SignalR Connection started'))
      .catch(err => console.log('Error while starting connection: ' + err));
  }

  public joinSession(sessionId: string) {
    if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
        this.hubConnection.invoke('JoinSession', sessionId)
            .catch(err => console.error(err));
    }
  }

  private addReceiveUpdateListener() {
    this.hubConnection.on('ReceiveUpdate', (sessionId: string, message: string) => {
      this.interviewUpdates$.next({ sessionId, message });
    });
  }

  public sendUpdate(sessionId: string, message: string) {
    if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
        this.hubConnection.invoke('SendInterviewUpdate', sessionId, message)
            .catch(err => console.error(err));
    }
  }
}
