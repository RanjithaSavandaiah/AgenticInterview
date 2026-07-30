import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { SetupInterviewComponent } from './setup-interview.component';

describe('SetupInterviewComponent', () => {
  let component: SetupInterviewComponent;
  let fixture: ComponentFixture<SetupInterviewComponent>;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SetupInterviewComponent],
      providers: [
        provideRouter([
          { path: 'interview/:sessionId', component: SetupInterviewComponent }
        ])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SetupInterviewComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  // --- Component Creation ---

  it('should create the component', () => {
    expect(component).toBeTruthy();
  });

  // --- Initial State ---

  describe('initial state', () => {
    it('should have null resume file', () => {
      expect(component.resumeFile()).toBeNull();
    });

    it('should have null JD file', () => {
      expect(component.jdFile()).toBeNull();
    });

    it('should not be loading', () => {
      expect(component.isLoading()).toBe(false);
    });

    it('should have no error message', () => {
      expect(component.errorMessage()).toBeNull();
    });
  });

  // --- File Selection ---

  describe('onFileSelected', () => {
    it('should set resume file when type is resume', async () => {
      const mockFile = new File(['resume content'], 'resume.pdf', { type: 'application/pdf' });
      const event = { target: { files: [mockFile] } } as unknown as Event;
      
      await component.onFileSelected(event, 'resume');
      
      expect(component.resumeFile()).toBeTruthy();
      expect(component.resumeFile()!.name).toBe('resume.pdf');
    });

    it('should set JD file when type is jd', async () => {
      const mockFile = new File(['job description'], 'jd.docx', { type: 'application/vnd.openxmlformats' });
      const event = { target: { files: [mockFile] } } as unknown as Event;
      
      await component.onFileSelected(event, 'jd');
      
      expect(component.jdFile()).toBeTruthy();
      expect(component.jdFile()!.name).toBe('jd.docx');
    });

    it('should clear error message on file selection', async () => {
      component.errorMessage.set('Some previous error');
      
      const mockFile = new File(['content'], 'test.pdf', { type: 'application/pdf' });
      const event = { target: { files: [mockFile] } } as unknown as Event;
      
      await component.onFileSelected(event, 'resume');
      
      expect(component.errorMessage()).toBeNull();
    });

    it('should handle empty file input gracefully', async () => {
      const event = { target: { files: [] } } as unknown as Event;
      await component.onFileSelected(event, 'resume');
      expect(component.resumeFile()).toBeNull();
    });

    it('should handle null files gracefully', async () => {
      const event = { target: { files: null } } as unknown as Event;
      await component.onFileSelected(event, 'resume');
      expect(component.resumeFile()).toBeNull();
    });
  });

  // --- Form Validation ---

  describe('startInterview validation', () => {
    it('should show error when resume is missing', async () => {
      const jdFile = new File(['jd'], 'jd.pdf');
      await component.onFileSelected({ target: { files: [jdFile] } } as unknown as Event, 'jd');
      
      await component.startInterview();
      
      expect(component.errorMessage()).toBe('Please upload both a resume and a job description.');
    });

    it('should show error when JD is missing', async () => {
      const resumeFile = new File(['resume'], 'resume.pdf');
      await component.onFileSelected({ target: { files: [resumeFile] } } as unknown as Event, 'resume');
      
      await component.startInterview();
      
      expect(component.errorMessage()).toBe('Please upload both a resume and a job description.');
    });

    it('should show error when both files are missing', async () => {
      await component.startInterview();
      expect(component.errorMessage()).toBe('Please upload both a resume and a job description.');
    });
  });

  // --- Upload Flow ---

  describe('startInterview upload', () => {
    beforeEach(async () => {
      // Set both files
      const resume = new File(['resume content'], 'resume.pdf', { type: 'application/pdf' });
      const jd = new File(['jd content'], 'jd.pdf', { type: 'application/pdf' });
      
      await component.onFileSelected({ target: { files: [resume] } } as unknown as Event, 'resume');
      await component.onFileSelected({ target: { files: [jd] } } as unknown as Event, 'jd');
    });

    it('should set isLoading when starting', async () => {
      // Mock fetch to delay
      const fetchSpy = vi.spyOn(globalThis, 'fetch').mockImplementation(
        () => new Promise(() => {}) // Never resolves
      );
      
      const promise = component.startInterview();
      expect(component.isLoading()).toBe(true);
      
      fetchSpy.mockRestore();
    });

    it('should navigate to interview room on success', async () => {
      const navigateSpy = vi.spyOn(router, 'navigate');
      
      vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve({ sessionId: 'new-session-xyz' })
      } as Response);
      
      await component.startInterview();
      
      expect(navigateSpy).toHaveBeenCalledWith(['/interview', 'new-session-xyz']);
      expect(component.isLoading()).toBe(false);
    });

    it('should show error message on API failure', async () => {
      vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce({
        ok: false,
        statusText: 'Internal Server Error',
        json: () => Promise.resolve({ Detail: 'File format not supported' })
      } as any);
      
      await component.startInterview();
      
      expect(component.errorMessage()).toContain('File format not supported');
      expect(component.isLoading()).toBe(false);
    });

    it('should show file-lock error for ERR_ACCESS_DENIED', async () => {
      vi.spyOn(globalThis, 'fetch').mockRejectedValueOnce(new Error('ERR_ACCESS_DENIED'));
      
      await component.startInterview();
      
      expect(component.errorMessage()).toContain('locked by another process');
      expect(component.errorMessage()).toContain('Microsoft Word');
    });

    it('should show file-lock error for Failed to fetch', async () => {
      vi.spyOn(globalThis, 'fetch').mockRejectedValueOnce(new Error('Failed to fetch'));
      
      await component.startInterview();
      
      expect(component.errorMessage()).toContain('locked by another process');
    });

    it('should show generic error for unknown failures', async () => {
      vi.spyOn(globalThis, 'fetch').mockRejectedValueOnce(new Error('Something went wrong'));
      
      await component.startInterview();
      
      expect(component.errorMessage()).toContain('Failed to start interview: Something went wrong');
    });

    it('should call fetch with correct URL and FormData', async () => {
      const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve({ sessionId: 'abc' })
      } as Response);
      
      await component.startInterview();
      
      expect(fetchSpy).toHaveBeenCalledWith('/api/setup/upload-and-start', expect.objectContaining({
        method: 'POST'
      }));
      
      const callArgs = fetchSpy.mock.calls[0];
      expect(callArgs[1]?.body).toBeInstanceOf(FormData);
    });
  });

  // --- Template Rendering ---

  describe('template rendering', () => {
    it('should show setup title', async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      
      const h1 = fixture.nativeElement.querySelector('h1');
      expect(h1?.textContent).toContain('Agentic Interview Setup');
    });

    it('should have resume upload input', async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      
      const input = fixture.nativeElement.querySelector('#resumeUpload');
      expect(input).toBeTruthy();
      expect(input?.getAttribute('accept')).toContain('.pdf');
    });

    it('should have JD upload input', async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      
      const input = fixture.nativeElement.querySelector('#jdUpload');
      expect(input).toBeTruthy();
    });

    it('should disable start button when files are not selected', async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      
      const btn = fixture.nativeElement.querySelector('.start-button');
      expect(btn?.disabled).toBe(true);
    });

    it('should enable start button when both files are selected', async () => {
      const resume = new File(['r'], 'r.pdf');
      const jd = new File(['j'], 'j.pdf');
      await component.onFileSelected({ target: { files: [resume] } } as unknown as Event, 'resume');
      await component.onFileSelected({ target: { files: [jd] } } as unknown as Event, 'jd');
      
      fixture.detectChanges();
      await fixture.whenStable();
      
      const btn = fixture.nativeElement.querySelector('.start-button');
      expect(btn?.disabled).toBe(false);
    });

    it('should show error message when present', async () => {
      component.errorMessage.set('Test error');
      fixture.detectChanges();
      await fixture.whenStable();
      
      const errorEl = fixture.nativeElement.querySelector('.error-message');
      expect(errorEl?.textContent).toContain('Test error');
    });

    it('should NOT show error message when null', async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      
      const errorEl = fixture.nativeElement.querySelector('.error-message');
      expect(errorEl).toBeFalsy();
    });

    it('should show loading spinner text when loading', async () => {
      component.isLoading.set(true);
      // Need files to not disable the button
      const resume = new File(['r'], 'r.pdf');
      const jd = new File(['j'], 'j.pdf');
      await component.onFileSelected({ target: { files: [resume] } } as unknown as Event, 'resume');
      await component.onFileSelected({ target: { files: [jd] } } as unknown as Event, 'jd');
      
      fixture.detectChanges();
      await fixture.whenStable();
      
      const spinner = fixture.nativeElement.querySelector('.spinner');
      expect(spinner?.textContent).toContain('Processing Documents');
    });

    it('should display selected file names', async () => {
      const resume = new File(['r'], 'my_resume.pdf');
      await component.onFileSelected({ target: { files: [resume] } } as unknown as Event, 'resume');
      
      fixture.detectChanges();
      await fixture.whenStable();
      
      const label = fixture.nativeElement.querySelector('label[for="resumeUpload"]');
      expect(label?.textContent).toContain('my_resume.pdf');
    });
  });

  // --- toBlob helper ---

  describe('toBlob', () => {
    it('should convert ArrayBuffer to Blob', () => {
      const buffer = new ArrayBuffer(8);
      const result = component['toBlob'](buffer);
      expect(result).toBeInstanceOf(Blob);
    });

    it('should pass through File as-is', () => {
      const file = new File(['content'], 'test.txt');
      const result = component['toBlob'](file);
      expect(result).toBe(file);
    });
  });
});
