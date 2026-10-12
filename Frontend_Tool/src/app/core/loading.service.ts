import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class LoadingService {
  private pending = 0;
  private aiPending = 0;
  private uploadPending = 0;
  private uploadProgressTimer?: ReturnType<typeof setInterval>;
  readonly uploadProgress = signal(0);
  readonly isVisible = signal(false);
  readonly isAiVisible = signal(false);
  readonly isUploadVisible = signal(false);

  start(isAi = false, isUpload = false) {
    this.pending++;
    if (isAi) { this.aiPending++; this.isAiVisible.set(true); }
    if (isUpload) {
      if (this.uploadPending === 0) {
        this.uploadProgress.set(0);
        this.uploadProgressTimer = setInterval(() => {
          const current = this.uploadProgress();
          if (current < 90) this.uploadProgress.set(Math.min(90, current + (current < 70 ? 2 : 1)));
        }, 500);
      }
      this.uploadPending++;
      this.isUploadVisible.set(true);
    }
    if (this.pending === 1) this.isVisible.set(true);
  }

  stop(isAi = false, isUpload = false) {
    this.pending = Math.max(0, this.pending - 1);
    if (isAi) { this.aiPending = Math.max(0, this.aiPending - 1); if (this.aiPending === 0) this.isAiVisible.set(false); }
    if (isUpload) {
      this.uploadPending = Math.max(0, this.uploadPending - 1);
      if (this.uploadPending === 0) {
        if (this.uploadProgressTimer) clearInterval(this.uploadProgressTimer);
        this.uploadProgressTimer = undefined;
        this.uploadProgress.set(0);
        this.isUploadVisible.set(false);
      }
    }
    if (this.pending === 0) {
      this.isVisible.set(false);
    }
  }

  setUploadProgress(value: number) {
    this.uploadProgress.set(Math.max(0, Math.min(100, Math.round(value))));
  }
}
