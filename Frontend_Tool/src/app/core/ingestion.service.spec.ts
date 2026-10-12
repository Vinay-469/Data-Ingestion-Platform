import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { IngestionService } from './ingestion.service';

describe('IngestionService', () => {
  let service: IngestionService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [IngestionService, provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(IngestionService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends upload and validation requests as multipart form data', () => {
    const file = new File(['a,b'], 'data.csv', { type: 'text/csv' });
    service.upload(file).subscribe();
    let request = http.expectOne('/api/uploads');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.get('file')).toBe(file);
    request.flush({});

    service.validateUpload(file).subscribe();
    request = http.expectOne('/api/uploads/validate');
    expect(request.request.body.get('file')).toBe(file);
    request.flush({});
  });

  it('sends confirmation, cancellation, history and workbook requests', fakeAsync(() => {
    service.confirmUpload('id', true, false, true).subscribe();
    let request = http.expectOne('/api/uploads/confirm-async');
    expect(request.request.body).toEqual({ validationId: 'id', keepNullRows: true, skipDuplicateRows: false, skipInvalidRows: true, selectedCustomColumns: [], mappings: [], selectedMetadataColumns: [] });
    request.flush({ jobId: 'job-1', status: 'Processing', progress: 1 });
    tick();
    request = http.expectOne('/api/uploads/jobs/job-1');
    request.flush({ jobId: 'job-1', status: 'Completed', progress: 100, result: { skippedRows: 0 } });
    tick();

    service.cancelValidation('id').subscribe();
    request = http.expectOne('/api/uploads/validate/id');
    expect(request.request.method).toBe('DELETE');
    request.flush({});

    service.history().subscribe();
    request = http.expectOne('/api/uploads');
    expect(request.request.method).toBe('GET');
    request.flush([]);

    service.sheets(4).subscribe();
    request = http.expectOne('/api/uploads/4/sheets');
    request.flush([]);

    service.columns(7).subscribe();
    request = http.expectOne('/api/uploads/sheets/7/columns');
    request.flush([]);
  }));

  it('builds row pagination parameters and dashboard endpoints', () => {
    service.rows(7, 2, 20, 'North').subscribe();
    let request = http.expectOne(item => item.url === '/api/uploads/sheets/7/rows');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('20');
    expect(request.request.params.get('search')).toBe('North');
    request.flush([]);

    service.dashboard().subscribe();
    request = http.expectOne('/api/dashboard');
    request.flush({});
    service.adminDashboard().subscribe();
    request = http.expectOne('/api/admin/dashboard');
    request.flush({});
  });

  it('creates download URLs and calls status/download/AI endpoints', () => {
    expect(service.downloadUrl(9)).toBe('/api/uploads/9/download');
    service.download(9).subscribe();
    let request = http.expectOne('/api/uploads/9/download');
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob());

    service.setStatus(9, false).subscribe();
    request = http.expectOne('/api/uploads/9/status');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ isActive: false });
    request.flush({});

    service.analyzeSchema(9).subscribe();
    request = http.expectOne('/api/ai/schema-analysis/9');
    request.flush({});
    service.askAi(9, 'What is this?').subscribe();
    request = http.expectOne('/api/ai/chat/9');
    expect(request.request.body).toEqual({ question: 'What is this?' });
    request.flush({});
    service.askHelp('How do I upload?').subscribe();
    request = http.expectOne('/api/ai/help');
    expect(request.request.body).toEqual({ question: 'How do I upload?' });
    request.flush({});
  });

  it('uses default row pagination arguments', () => {
    service.rows(7).subscribe();
    const request = http.expectOne(item => item.url === '/api/uploads/sheets/7/rows');
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('50');
    expect(request.request.params.get('search')).toBe('');
    request.flush([]);
  });
});
