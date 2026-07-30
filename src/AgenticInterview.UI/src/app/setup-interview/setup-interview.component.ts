import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';

interface SelectedFile {
  name: string;
  /** Pre-read bytes (if eager read succeeded), or the raw File reference as fallback. */
  source: ArrayBuffer | File;
}

@Component({
  selector: 'app-setup-interview',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './setup-interview.component.html',
  styleUrls: ['./setup-interview.component.css']
})
export class SetupInterviewComponent {
  private router = inject(Router);

  resumeFile = signal<SelectedFile | null>(null);
  jdFile = signal<SelectedFile | null>(null);
  isLoading = signal(false);
  errorMessage = signal<string | null>(null);

  async onFileSelected(event: Event, type: 'resume' | 'jd') {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) return;

    const file = input.files[0];
    let source: ArrayBuffer | File;

    try {
      // Try to eagerly read the file bytes.
      // This catches lock issues early and gives a clear error message.
      source = await file.arrayBuffer();
    } catch {
      // Eager read failed (file locked by Word, OneDrive, etc.).
      // Fall back to storing the File reference — the browser will retry
      // the OS-level read when FormData serialises the request body.
      console.warn(
        `Eager read of "${file.name}" failed (file may be locked). ` +
        `Will attempt to read at upload time.`
      );
      source = file;
    }

    const selected: SelectedFile = { name: file.name, source };

    if (type === 'resume') {
      this.resumeFile.set(selected);
    } else {
      this.jdFile.set(selected);
    }
    this.errorMessage.set(null);
  }

  async startInterview() {
    const resume = this.resumeFile();
    const jd = this.jdFile();

    if (!resume || !jd) {
      this.errorMessage.set('Please upload both a resume and a job description.');
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const formData = new FormData();
      formData.append('candidateResume', this.toBlob(resume.source), resume.name);
      formData.append('jobDescription', this.toBlob(jd.source), jd.name);

      const response = await fetch('/api/setup/upload-and-start', {
        method: 'POST',
        body: formData
      });

      if (!response.ok) {
        const errorBody = await response.json().catch(() => null);
        const detail = errorBody?.Detail ?? errorBody?.detail ?? response.statusText;
        throw new Error(detail);
      }

      const result = await response.json();
      this.isLoading.set(false);
      this.router.navigate(['/interview', result.sessionId]);
    } catch (err: any) {
      this.isLoading.set(false);
      const msg = err?.message ?? 'Unknown error';

      // Detect file-lock related failures and show actionable guidance
      if (msg === 'Failed to fetch' || msg.includes('ERR_ACCESS_DENIED') || msg.includes('NotReadableError')) {
        this.errorMessage.set(
          `Cannot read the uploaded file — it is locked by another process. ` +
          `Please fully close Microsoft Word (check the taskbar and system tray), ` +
          `then re-select the file and try again.`
        );
      } else {
        this.errorMessage.set(`Failed to start interview: ${msg}`);
      }
      console.error('Setup error:', err);
    }
  }

  /** Convert an ArrayBuffer or File into a Blob suitable for FormData. */
  private toBlob(source: ArrayBuffer | File): Blob {
    return source instanceof ArrayBuffer ? new Blob([source]) : source;
  }
}
