import { Component, OnInit, OnDestroy, HostListener, inject, effect, ViewChild, ElementRef, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { MonacoEditorModule } from 'ngx-monaco-editor-v2';
import { InterviewStore } from '../state/interview.store';

declare var window: any;

@Component({
  selector: 'app-interview-room',
  standalone: true,
  imports: [CommonModule, FormsModule, MonacoEditorModule],
  templateUrl: './interview-room.html',
  styleUrls: ['./interview-room.css']
})
export class InterviewRoom implements OnInit, OnDestroy, AfterViewInit {
  editorOptions = {theme: 'vs-dark', language: 'csharp'};
  code: string = 'using System;\n\npublic class Solution {\n    public string ReverseString(string s) {\n        // Your code here\n        return s;\n    }\n}';
  
  isRecording = false;
  textInput = '';
  private route = inject(ActivatedRoute);
  
  sessionId = this.route.snapshot.paramMap.get('sessionId') || crypto.randomUUID();
  hubUrl = '/interviewhub';

  private recognition: any;
  public store = inject(InterviewStore);

  private previousMessageCount = 0;

  @ViewChild('candidateVideo') candidateVideo!: ElementRef<HTMLVideoElement>;
  isCameraActive = false;
  isInterviewReady = false;
  mediaStream: MediaStream | null = null;

  constructor() {
    // Sync local code with store on init
    this.store.setCandidateCode(this.code);

    // Watch for new AI messages and speak them using TTS
    effect(() => {
      const messages = this.store.messages();
      if (messages.length > this.previousMessageCount) {
        const newMessage = messages[messages.length - 1];
        
        // If the message is from an AI Agent, speak it
        if (newMessage.sourceAgent !== 'System' && newMessage.sourceAgent !== 'Candidate') {
          this.speakMessage(newMessage.content);
        }
      }
      this.previousMessageCount = messages.length;
    });

    // Sync microphone listening state with agent speaking state
    effect(() => {
      const agentIsSpeaking = this.store.isAgentSpeaking();
      if (this.recognition) {
        if (agentIsSpeaking) {
          // Pause candidate mic while agent talks
          this.isRecording = false;
          try { this.recognition.stop(); } catch(e) {}
        } else {
          // Resume candidate mic when agent finishes
          this.isRecording = true;
          try { this.recognition.start(); } catch(e) {}
        }
      }
    });
  }

  ngOnInit() {
    document.addEventListener('visibilitychange', this.handleVisibilityChange.bind(this));
  }

  ngAfterViewInit() {
    // Wait for user to grant permissions
  }

  async requestPermissions() {
    try {
      this.mediaStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
      this.isCameraActive = true;
      
      if (this.candidateVideo?.nativeElement) {
        this.candidateVideo.nativeElement.srcObject = this.mediaStream;
        this.candidateVideo.nativeElement.muted = true;
        this.candidateVideo.nativeElement.volume = 0;
      }
    } catch (err) {
      console.error('Failed to get media devices', err);
      alert('Camera and microphone access is required to take this interview.');
    }
  }

  private faceDetectionInterval: any = null;
  private lastFaceReport = 0;

  async startLiveInterview() {
    this.isInterviewReady = true;
    this.setupSpeechRecognition();
    
    // Explicitly start the microphone as soon as the interview starts, 
    // unless the agent immediately begins speaking.
    if (!this.store.isAgentSpeaking() && this.recognition) {
      this.isRecording = true;
      try { this.recognition.start(); } catch(e) {}
    }

    await this.store.startSession('/hrhub', this.sessionId);
    this.store.fetchSessionStatus(this.sessionId);
    this.store.fetchMessages(this.sessionId);

    // Start face detection if the FaceDetector API is available
    this.startFaceDetection();
  }

  ngOnDestroy() {
    if (this.recognition) {
      this.recognition.stop();
    }
    if (this.mediaStream) {
      this.mediaStream.getTracks().forEach(track => track.stop());
    }
    if (this.faceDetectionInterval) {
      clearInterval(this.faceDetectionInterval);
    }
    document.removeEventListener('visibilitychange', this.handleVisibilityChange.bind(this));
  }

  private async startFaceDetection() {
    // Check if the FaceDetector API is available (Chromium-based browsers only)
    if (!('FaceDetector' in window)) {
      console.warn('FaceDetector API not available. Face detection will be skipped.');
      return;
    }

    try {
      const detector = new (window as any).FaceDetector({ fastMode: true, maxDetectedFaces: 5 });
      
      this.faceDetectionInterval = setInterval(async () => {
        if (!this.candidateVideo?.nativeElement || !this.isCameraActive) return;
        
        const video = this.candidateVideo.nativeElement;
        if (video.readyState < 2) return; // Video not ready yet

        try {
          const faces = await detector.detect(video);
          
          if (faces.length > 1) {
            const now = Date.now();
            // Debounce: only report once every 10 seconds
            if (now - this.lastFaceReport > 10000) {
              this.lastFaceReport = now;
              console.warn(`Multiple faces detected: ${faces.length}`);
              this.reportMalpractice('MULTIPLE_FACES');
            }
          }
        } catch (err) {
          // Detection can fail on some frames — just skip silently
        }
      }, 3000); // Check every 3 seconds
    } catch (err) {
      console.warn('Failed to initialize FaceDetector:', err);
    }
  }

  setupSpeechRecognition() {
    const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (SpeechRecognition) {
      this.recognition = new SpeechRecognition();
      this.recognition.continuous = true;
      this.recognition.interimResults = true;
      this.recognition.lang = 'en-US';

      this.recognition.onresult = (event: any) => {
        let interimTranscript = '';
        let finalTranscript = '';

        for (let i = event.resultIndex; i < event.results.length; i++) {
          const transcript = event.results[i][0].transcript;
          if (event.results[i].isFinal) {
            finalTranscript += transcript;
            this.store.submitAnswer(this.sessionId, finalTranscript);
            console.log("Final Transcript:", finalTranscript);
          } else {
            interimTranscript += transcript;
          }
        }
      };

      this.recognition.onerror = (event: any) => {
        console.error("Speech recognition error", event.error);
        if (event.error === 'not-allowed') {
          this.isRecording = false;
        }
      };

      this.recognition.onend = () => {
        // Automatically restart if we aren't paused by the agent speaking
        if (!this.store.isAgentSpeaking() && this.isInterviewReady && this.isRecording !== false) {
          try { this.recognition.start(); } catch(e) {}
        }
      };
    } else {
      console.warn("Speech recognition not supported in this browser.");
    }
  }

  speakMessage(text: string) {
    if (!('speechSynthesis' in window)) return;

    const utterance = new SpeechSynthesisUtterance(text);
    
    // Try to find a good English voice
    const voices = window.speechSynthesis.getVoices();
    const preferredVoice = voices.find((v: any) => v.lang.includes('en-') && v.name.includes('Google'));
    if (preferredVoice) utterance.voice = preferredVoice;

    utterance.rate = 1.0;
    utterance.pitch = 1.0;

    utterance.onstart = () => {
      this.store.setAgentSpeaking(true);
    };

    utterance.onend = () => {
      this.store.setAgentSpeaking(false);
    };

    utterance.onerror = (e) => {
      console.error('SpeechSynthesis error', e);
      this.store.setAgentSpeaking(false);
    };

    window.speechSynthesis.speak(utterance);
  }

  sendText() {
    if (this.textInput.trim()) {
      this.store.submitAnswer(this.sessionId, this.textInput);
      this.textInput = '';
    }
  }

  evaluateCode() {
    this.store.submitCode(this.sessionId, this.store.candidateCode());
    console.log("Submitting code for evaluation");
  }

  // --- Proctoring & Malpractice Detection ---

  handleVisibilityChange() {
    if (document.visibilityState === 'hidden') {
      this.reportMalpractice('TAB_SWITCH');
    }
  }

  @HostListener('window:blur')
  onWindowBlur() {
    this.reportMalpractice('WINDOW_BLUR');
  }

  @HostListener('copy', ['$event'])
  onCopy(event: ClipboardEvent) {
    this.reportMalpractice('COPY_ATTEMPT');
  }

  @HostListener('paste', ['$event'])
  onPaste(event: ClipboardEvent) {
    this.reportMalpractice('PASTE_ATTEMPT');
  }

  private reportMalpractice(type: string) {
    console.warn(`Malpractice detected: ${type}`);
    this.store.reportMalpractice(this.sessionId, type);
  }
}
