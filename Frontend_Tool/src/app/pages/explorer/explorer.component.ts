import { Component } from '@angular/core';
import { DecimalPipe, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { IngestionService } from '../../core/ingestion.service';
import { UploadSummary } from '../../core/models';

@Component({ selector: 'app-explorer', standalone: true, imports: [RouterLink, DecimalPipe, DatePipe], templateUrl: './explorer.component.html', styleUrl: './explorer.component.css' })
export class ExplorerComponent {
  uploads: UploadSummary[] = [];
  loading = true;
  selected?: UploadSummary;
  constructor(readonly ingestion: IngestionService) { this.ingestion.history().subscribe({ next: value => { this.uploads = value; this.selected = value[0]; this.loading = false; }, error: () => this.loading = false }); }
  get totalRows() { return this.uploads.reduce((sum, upload) => sum + upload.totalRows, 0); }
  get totalSheets() { return this.uploads.reduce((sum, upload) => sum + upload.totalSheets, 0); }
  get totalDuplicates() { return this.uploads.reduce((sum, upload) => sum + upload.duplicateRows, 0); }
  get maximumRows() { return Math.max(...this.uploads.map(upload => upload.totalRows), 1); }
  rowWidth(upload: UploadSummary) { return upload.totalRows / this.maximumRows * 100; }
  select(upload: UploadSummary) { this.selected = upload; }
}
