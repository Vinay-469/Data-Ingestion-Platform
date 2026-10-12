import { Injectable } from '@angular/core';
import { HttpClient, HttpContext, HttpEventType, HttpParams, HttpResponse } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { AdminDashboard, AiSchemaAnalysis, Column, ColumnMappingResult, ConfirmedColumnMapping, Dashboard, MetadataColumnSelection, Row, Sheet, StandardColumnCoverage, UploadJobStatus, UploadSummary, UploadValidationResult, ValidationSummary } from './models';
import { catchError, concat, filter, map, Observable, of, shareReplay, switchMap, takeWhile, tap, throwError, timer } from 'rxjs';
import { LoadingService } from './loading.service';
import { SKIP_GLOBAL_LOADING } from './auth.interceptor';
@Injectable({ providedIn: 'root' })
export class IngestionService {
  private readonly base = `${environment.apiUrl}/uploads`;
  private readonly cache = new Map<string, { expires: number; request: Observable<unknown> }>();
  private readonly mappingCache = new Map<string, Observable<ColumnMappingResult>>();
  private readonly cacheTtlMs = 60_000;
  constructor(private readonly http: HttpClient, private readonly loading: LoadingService) {}
  private storageKey(key: string) { const user = localStorage.getItem('ingestion.user') || 'anonymous'; const role = localStorage.getItem('ingestion.role') || 'Customer'; return `ingestion.cache.${user}.${role}.${key}`; }
  private memoryKey(key: string) { const user = localStorage.getItem('ingestion.user') || 'anonymous'; const role = localStorage.getItem('ingestion.role') || 'Customer'; return `${user}:${role}:${key}`; }
  private canPersist(key: string) { return key === 'dashboard' || key === 'admin-dashboard-v3' || key === 'history'; }
  private readPersisted<T>(key: string) { if (!this.canPersist(key)) return undefined; try { const raw = sessionStorage.getItem(this.storageKey(key)); return raw ? JSON.parse(raw) as { expires: number; data: T } : undefined; } catch { return undefined; } }
  private writePersisted<T>(key: string, data: T, expires: number) { if (!this.canPersist(key)) return; try { sessionStorage.setItem(this.storageKey(key), JSON.stringify({ expires, data })); } catch { /* Storage quotas must not block page rendering. */ } }
  private cached<T>(key: string, factory: () => Observable<T>, ttl = this.cacheTtlMs, backgroundFactory = factory) {
    const now = Date.now();
    const scopedKey = this.memoryKey(key);
    const existing = this.cache.get(scopedKey);
    if (existing && existing.expires > now) return existing.request as Observable<T>;
    const persisted = this.readPersisted<T>(key);
    const expires = now + ttl;
    if (persisted?.data !== undefined) {
      let request!: Observable<T>;
      const refresh = backgroundFactory().pipe(tap(value => this.writePersisted(key, value, expires)), catchError(() => of(persisted.data)));
      request = concat(of(persisted.data), refresh).pipe(shareReplay({ bufferSize: 1, refCount: false }));
      this.cache.set(scopedKey, { expires, request });
      return request;
    }
    let request!: Observable<T>;
    request = factory().pipe(tap(value => this.writePersisted(key, value, expires)), catchError(error => { if (this.cache.get(scopedKey)?.request === request) this.cache.delete(scopedKey); return throwError(() => error); }), shareReplay({ bufferSize: 1, refCount: false }));
    this.cache.set(scopedKey, { expires, request });
    return request;
  }
  refreshCache() { this.cache.clear(); this.mappingCache.clear(); for (const key of ['dashboard', 'admin-dashboard', 'admin-dashboard-v2', 'admin-dashboard-v3', 'history']) { try { sessionStorage.removeItem(this.storageKey(key)); } catch { /* Ignore unavailable browser storage. */ } } }
  upload(file: File) { const form = new FormData(); form.append('file', file); return this.http.post(`${this.base}`, form).pipe(tap(() => this.refreshCache())); }
  validateUpload(file: File) {
    const form = new FormData(); form.append('file', file);
    return this.http.post<UploadValidationResult>(`${this.base}/validate`, form, { observe: 'events', reportProgress: true }).pipe(
      tap(event => {
        if (event.type === HttpEventType.Sent) this.loading.setUploadProgress(3);
        if (event.type === HttpEventType.UploadProgress) {
          const total = event.total || file.size || 1;
          this.loading.setUploadProgress(5 + (event.loaded / total) * 45);
        }
        if (event.type === HttpEventType.Response) this.loading.setUploadProgress(70);
      }),
      filter((event): event is HttpResponse<UploadValidationResult> => event.type === HttpEventType.Response),
      map(event => event.body as UploadValidationResult),
    );
  }
  mapColumns(validationId: string) {
    const existing = this.mappingCache.get(validationId);
    if (existing) return existing;
    const request = this.http.post<ColumnMappingResult>(`${environment.apiUrl}/ai/column-mapping/${validationId}`, {}).pipe(
      catchError(error => { this.mappingCache.delete(validationId); return throwError(() => error); }),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    this.mappingCache.set(validationId, request);
    return request;
  }
  importJob(validationId: string, keepNullRows: boolean, skipDuplicateRows: boolean, skipInvalidRows: boolean, selectedCustomColumns: string[] = [], mappings: ConfirmedColumnMapping[] = [], selectedMetadataColumns: MetadataColumnSelection[] = [], excludedSheetOrders: number[] = []) {
    this.loading.setUploadProgress(1);
    return this.http.post<UploadJobStatus>(`${this.base}/confirm-async`, { validationId, keepNullRows, skipDuplicateRows, skipInvalidRows, selectedCustomColumns, mappings, selectedMetadataColumns, ...(excludedSheetOrders.length ? { excludedSheetOrders } : {}) }).pipe(
      switchMap(job => timer(0, 1000).pipe(switchMap(() => this.uploadJob(job.jobId)), tap(status => this.loading.setUploadProgress(status.progress)), takeWhile(status => status.status === 'Processing', true))),
      filter(status => status.status !== 'Processing'),
    );
  }
  confirmUpload(validationId: string, keepNullRows: boolean, skipDuplicateRows: boolean, skipInvalidRows: boolean, selectedCustomColumns: string[] = [], mappings: ConfirmedColumnMapping[] = [], selectedMetadataColumns: MetadataColumnSelection[] = [], excludedSheetOrders: number[] = []) {
    return this.importJob(validationId, keepNullRows, skipDuplicateRows, skipInvalidRows, selectedCustomColumns, mappings, selectedMetadataColumns, excludedSheetOrders).pipe(
      switchMap(status => status.status === 'Completed' && status.result ? [status.result] : throwError(() => new Error(status.errorMessage ?? 'The import could not be completed.'))),
      tap(() => this.refreshCache()),
    );
  }
  uploadJob(jobId: string) { return this.http.get<UploadJobStatus>(`${this.base}/jobs/${jobId}`); }
  cancelJob(jobId: string) { return this.http.delete<UploadJobStatus>(`${this.base}/jobs/${jobId}`); }
  cancelValidation(validationId: string) { return this.http.delete(`${this.base}/validate/${validationId}`); }
  history() { return this.cached('history', () => this.http.get<UploadSummary[]>(this.base), this.cacheTtlMs, () => this.http.get<UploadSummary[]>(this.base, { context: new HttpContext().set(SKIP_GLOBAL_LOADING, true) })); }
  sheets(uploadId: number) { return this.cached(`sheets:${uploadId}`, () => this.http.get<Sheet[]>(`${this.base}/${uploadId}/sheets`)); }
  standardCoverage(uploadId: number) { return this.cached(`standard-coverage:${uploadId}`, () => this.http.get<StandardColumnCoverage[]>(`${this.base}/${uploadId}/standard-coverage`), 60_000); }
  columns(sheetId: number) { return this.cached(`columns:${sheetId}`, () => this.http.get<Column[]>(`${this.base}/sheets/${sheetId}/columns`)); }
  rows(sheetId: number, page = 1, pageSize = 50, search = '') { const params = new HttpParams().set('page', page).set('pageSize', pageSize).set('search', search); return this.cached(`rows:${sheetId}:${page}:${pageSize}:${search}`, () => this.http.get<Row[]>(`${this.base}/sheets/${sheetId}/rows`, { params }), 5_000); }
  dashboard() { return this.cached('dashboard', () => this.http.get<Dashboard>(`${environment.apiUrl}/dashboard`), this.cacheTtlMs, () => this.http.get<Dashboard>(`${environment.apiUrl}/dashboard`, { context: new HttpContext().set(SKIP_GLOBAL_LOADING, true) })); }
  adminDashboard() { return this.cached('admin-dashboard-v3', () => this.http.get<AdminDashboard>(`${environment.apiUrl}/admin/dashboard`), this.cacheTtlMs, () => this.http.get<AdminDashboard>(`${environment.apiUrl}/admin/dashboard`, { context: new HttpContext().set(SKIP_GLOBAL_LOADING, true) })); }
  downloadUrl(uploadId: number) { return `${this.base}/${uploadId}/download`; }
  download(uploadId: number) { return this.http.get(this.downloadUrl(uploadId), { responseType: 'blob' }); }
  setStatus(uploadId: number, isActive: boolean) { return this.http.put(`${this.base}/${uploadId}/status`, { isActive }).pipe(tap(() => this.refreshCache())); }
  analyzeSchema(uploadId: number) { return this.http.get<AiSchemaAnalysis>(`${environment.apiUrl}/ai/schema-analysis/${uploadId}`); }
  askAi(uploadId: number, question: string) { return this.http.post<{ uploadId: number; model: string; metadataOnly: boolean; answer: string; generatedOn: string }>(`${environment.apiUrl}/ai/chat/${uploadId}`, { question }); }
  askHelp(question: string) { return this.http.post<{ model: string; answer: string; generatedOn: string }>(`${environment.apiUrl}/ai/help`, { question }); }
}
