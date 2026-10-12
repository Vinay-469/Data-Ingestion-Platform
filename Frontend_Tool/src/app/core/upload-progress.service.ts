import { Injectable, signal } from '@angular/core';
import { Subject, Subscription, timer } from 'rxjs';
import { IngestionService } from './ingestion.service';
import { UploadJobStatus } from './models';
import { ConfirmedColumnMapping, MetadataColumnSelection } from './models';

export interface UploadProgressState { fileName: string; stage: 'Uploading' | 'Importing' | 'Completed' | 'Failed' | 'Cancelled'; progress: number; elapsedSeconds: number; jobId?: string; message: string; error?: string; }

@Injectable({ providedIn: 'root' })
export class UploadProgressService {
  readonly state = signal<UploadProgressState | null>(null);
  readonly completed = new Subject<void>();
  private subscription?: Subscription;
  private elapsedSubscription?: Subscription;
  constructor(private readonly ingestion: IngestionService) {}
  startUpload(fileName: string) { this.stopElapsedTimer(); this.state.set({ fileName, stage: 'Uploading', progress: 5, elapsedSeconds: 0, message: 'Uploading and validating your file...' }); this.startElapsedTimer(); }
  reviewReady() { if (this.state()?.stage === 'Uploading') this.dismiss(); }
  uploadFailed(message: string) { const current = this.state(); if (current) this.state.set({ ...current, stage: 'Failed', progress: 0, message: 'File validation failed.', error: message }); this.stopElapsedTimer(); }
  startImport(fileName: string, validationId: string, keepNullRows: boolean, skipDuplicateRows: boolean, skipInvalidRows: boolean, selectedCustomColumns: string[] = [], mappings: ConfirmedColumnMapping[] = [], selectedMetadataColumns: MetadataColumnSelection[] = [], excludedSheetOrders: number[] = []) { this.subscription?.unsubscribe(); this.stopElapsedTimer(); this.state.set({ fileName, stage: 'Importing', progress: 1, elapsedSeconds: 0, message: 'Starting import...' }); this.startElapsedTimer(); this.subscription = this.ingestion.importJob(validationId, keepNullRows, skipDuplicateRows, skipInvalidRows, selectedCustomColumns, mappings, selectedMetadataColumns, excludedSheetOrders).subscribe({ next: status => this.applyStatus(status), error: error => { this.stopElapsedTimer(); this.state.set({ fileName, stage: 'Failed', progress: 0, elapsedSeconds: this.state()?.elapsedSeconds ?? 0, message: 'Import could not be completed.', error: error?.error?.message ?? error?.message ?? 'The import failed.' }); } }); }
  cancel() { const current = this.state(); if (!current?.jobId || current.stage !== 'Importing') return; this.state.update(value => value ? { ...value, message: 'Cancelling import...' } : value); this.ingestion.cancelJob(current.jobId).subscribe({ next: () => { this.subscription?.unsubscribe(); this.subscription = undefined; this.stopElapsedTimer(); this.state.set({ ...current, stage: 'Cancelled', progress: 0, message: 'Import cancelled. Temporary and partial data were removed.' }); }, error: () => this.state.update(value => value ? { ...value, message: 'Cancellation request could not be sent.' } : value) }); }
  dismiss() { this.subscription?.unsubscribe(); this.subscription = undefined; this.stopElapsedTimer(); this.state.set(null); }
  private applyStatus(status: UploadJobStatus) { const current = this.state(); if (!current) return; if (status.status === 'Completed') { this.subscription = undefined; this.ingestion.refreshCache(); this.stopElapsedTimer(); this.state.set(null); this.completed.next(); return; } const message = status.status === 'Cancelled' ? 'Import cancelled.' : status.status === 'Failed' ? 'Import failed.' : 'Importing your file in the background...'; this.state.set({ ...current, jobId: status.jobId, progress: status.progress, stage: status.status === 'Processing' ? 'Importing' : status.status, message, error: status.errorMessage }); if (status.status !== 'Processing') { this.subscription = undefined; this.stopElapsedTimer(); } }
  private startElapsedTimer() { const started = Date.now(); this.elapsedSubscription = timer(0, 1000).subscribe(() => { const current = this.state(); if (current) this.state.set({ ...current, elapsedSeconds: Math.floor((Date.now() - started) / 1000) }); }); }
  private stopElapsedTimer() { this.elapsedSubscription?.unsubscribe(); this.elapsedSubscription = undefined; }
}
