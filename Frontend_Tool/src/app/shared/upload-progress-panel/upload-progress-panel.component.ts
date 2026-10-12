import { Component } from '@angular/core';
import { UploadProgressService } from '../../core/upload-progress.service';

@Component({ selector: 'app-upload-progress-panel', standalone: true, templateUrl: './upload-progress-panel.component.html', styleUrl: './upload-progress-panel.component.css' })
export class UploadProgressPanelComponent {
  constructor(readonly progress: UploadProgressService) {}
  cancel() { this.progress.cancel(); }
  dismiss() { this.progress.dismiss(); }
}
