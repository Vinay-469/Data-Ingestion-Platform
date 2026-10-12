import { of, throwError } from 'rxjs';
import { DashboardComponent } from './dashboard/dashboard.component';
import { ExplorerComponent } from './explorer/explorer.component';
import { HistoryComponent } from './history/history.component';
import { LoginComponent } from './login/login.component';
import { ViewerComponent } from './viewer/viewer.component';
import { UploadValidationDialogComponent } from '../shared/upload-validation-dialog/upload-validation-dialog.component';
import { UploadSummary, Sheet, Column, Row, DailyUploadSeries, UploadValidationResult } from '../core/models';

const upload = (id = 1, name = 'data.csv', rows = 10): UploadSummary => ({
  uploadId: id, userName: 'user', originalFileName: name, status: 'Completed', isActive: true,
  uploadedOn: '2026-08-18T10:00:00Z', totalSheets: 2, totalRows: rows, duplicateRows: 1, nullRows: 2
});

const columns: Column[] = [
  { columnId: 1, columnName: 'Facility', displayName: 'Facility', columnOrder: 0, detectedDataType: 'string', allowNull: true, uniqueValues: 2 },
  { columnId: 2, columnName: 'Quantity', displayName: 'Quantity', columnOrder: 1, detectedDataType: 'number', allowNull: false, uniqueValues: 3 },
  { columnId: 3, columnName: 'Active', displayName: 'Active', columnOrder: 2, detectedDataType: 'boolean', allowNull: true, uniqueValues: 2 }
];

const rows: Row[] = [
  { rowId: 1, rowNumber: 2, hasDuplicate: false, hasNull: false, isValid: true, cells: [
    { columnId: 1, columnName: 'Facility', value: 'North', detectedDataType: 'string', validationStatus: 'Valid' },
    { columnId: 2, columnName: 'Quantity', value: '10', detectedDataType: 'number', validationStatus: 'Valid' },
    { columnId: 3, columnName: 'Active', value: 'true', detectedDataType: 'boolean', validationStatus: 'Valid' }
  ] },
  { rowId: 2, rowNumber: 3, hasDuplicate: true, hasNull: true, isValid: false, cells: [
    { columnId: 1, columnName: 'Facility', value: 'South', detectedDataType: 'string', validationStatus: 'Valid' },
    { columnId: 2, columnName: 'Quantity', value: '20', detectedDataType: 'number', validationStatus: 'Valid' }
  ] }
];

const series: DailyUploadSeries[] = [{ label: 'All uploads', points: [{ date: '2026-08-17', count: 2 }, { date: '2026-08-18', count: 4 }] }];

describe('DashboardComponent', () => {
  function createDashboard(role = 'Customer', ingestionOverrides: any = {}) {
    const ingestion = {
      dashboard: () => of({ totalUploads: 2, filesUploaded: 2, rowsImported: 30, sheetsImported: 3, duplicateRows: 1, nullRows: 2, successRate: 90, failureRate: 10, uploadTrendPercent: 2, rowsTrendPercent: null, dailyUploads: series }),
      history: () => of([upload(), upload(2, 'second.csv', 20)]),
      adminDashboard: () => of({ totalUsers: 4, totalUploads: 2, activeUploads: 2, inactiveUploads: 0, rowsImported: 30, sheetsImported: 3, duplicateRows: 1, nullRows: 2, successRate: 90, failureRate: 10, dailyUploads: series, recentUploads: [] }),
      validateUpload: () => of({} as UploadValidationResult),
      mapColumns: () => of({ validationId: 'v', sourceColumns: [], mappings: [], targetColumns: [], aiAvailable: true }),
      cancelValidation: () => of({}),
      confirmUpload: () => of({ skippedRows: 0 }),
      askHelp: () => of({ answer: 'help answer' }),
      download: () => of(new Blob(['data'])),
      ...ingestionOverrides
    };
    const auth = { role: () => role };
    const theme = { isDark: () => false, toggle: jasmine.createSpy('toggle') };
    const uploadProgress = { completed: of(undefined), startUpload: jasmine.createSpy('startUpload'), reviewReady: jasmine.createSpy('reviewReady'), uploadFailed: jasmine.createSpy('uploadFailed'), startImport: jasmine.createSpy('startImport') };
    const router = { navigate: jasmine.createSpy('navigate') };
    return { component: new DashboardComponent(auth as any, ingestion as any, theme as any, {} as any, uploadProgress as any, router as any), ingestion, auth, router };
  }

  it('loads customer/admin data and exposes chart helpers', () => {
    const { component } = createDashboard('Admin');
    expect(component.uploads.length).toBe(2);
    expect(component.adminDashboard?.totalUsers).toBe(4);
    expect(component.dashboard?.totalUploads).toBe(2);
    expect(component.barWidth(10, component.uploads)).toBe(50);
    expect(component.barWidth(0, [])).toBe(0);
    expect(component.progressRing(10, component.uploads)).toContain('conic-gradient');
    expect(component.chartSeries(undefined)).toEqual([]);
    expect(component.chartPoints(series[0], series)).toContain(',');
    expect(component.smoothChartPath({ label: 'one', points: [{ date: '2026-01-01', count: 1 }] }, series)).toContain('M ');
    expect(component.smoothChartPath({ label: 'none', points: [] }, series)).toBe('');
    expect(component.chartMax(series)).toBe(4);
    expect(component.chartX(0, 1)).toBe(42);
    expect(component.chartY(4, series)).toBe(35);
    expect(component.dailyTotal(series[0])).toBe(6);
    expect(component.dailyPeak(series[0])).toBe(4);
    expect(component.chartTick(series, 0.5)).toBe(2);
    expect(component.chartDate(series[0], 0)).toContain('17');
    expect(component.formatSize(5)).toBe('5 B');
    expect(component.formatSize(2048)).toBe('2.0 KB');
    expect(component.formatSize(2 * 1024 * 1024)).toBe('2.0 MB');
    expect(component.formatTrend(2.15)).toBe('+2.1%');
    expect(component.formatTrend(-2)).toBe('-2.0%');
    expect(component.formatTrend(null)).toBe('—');
    expect(component.trendClass(2)).toBe('trend positive');
    expect(component.trendClass(-2)).toBe('trend negative');
    expect(component.trendClass()).toBe('trend neutral');
    expect(component.qualityGradient(150)).toContain('100%');
    expect(['Good morning', 'Good afternoon', 'Good evening', 'Good night']).toContain(component.greeting);
  });

  it('handles navigation, help, selection, upload validation and confirmation', () => {
    const { component, ingestion, router } = createDashboard();
    component.selectUpload(upload());
    component.openUpload(upload(3, 'open.csv'));
    expect(router.navigate).toHaveBeenCalledWith(['/viewer', 3]);
    component.openHelp();
    expect(component.helpOpen).toBeTrue();
    component.helpQuestion = '  how?  ';
    component.askHelp();
    expect(component.helpAnswer).toBe('help answer');
    component.closeHelp();
    expect(component.helpOpen).toBeFalse();
    component.selectedFile = new File(['a'], 'data-1.csv', { type: 'text/csv' });
    component.validation = { validationId: 'v', duplicateFile: true } as UploadValidationResult;
    component.decideValidation({ keepNullRows: true, skipDuplicateRows: false, skipInvalidRows: false, renameDuplicate: true, selectedCustomColumns: [], mappings: [], selectedMetadataColumns: [] });
    expect(component.isUploading).toBeFalse();
    component.validation = { validationId: 'v', duplicateFile: false } as UploadValidationResult;
    component.decideValidation({ keepNullRows: true, skipDuplicateRows: true, skipInvalidRows: false, renameDuplicate: false, selectedCustomColumns: [], mappings: [], selectedMetadataColumns: [] });
    expect(component.message).toBe('Import started in the background.');
    component.decideValidation(null);
    expect(component.validation).toBeUndefined();
    component.selectedFile = new File(['a'], 'data-2.csv', { type: 'text/csv' });
    component.select({ target: { files: [component.selectedFile] } } as any);
    component.drop({ preventDefault: jasmine.createSpy(), dataTransfer: { files: [component.selectedFile] } } as any);
    component.upload();
    expect(component.validation).toBeDefined();
    component.refresh();
    expect(component.message).toBe('');
    expect((component as any).withDuplicateSuffix(new File(['x'], 'report-2.csv'), 1).name).toBe('report-3.csv');
  });

  it('handles request failures and empty-input branches', () => {
    const failed = createDashboard('Customer', {
      dashboard: () => throwError(() => new Error('down')),
      validateUpload: () => throwError(() => ({ error: { message: 'invalid' } })),
      askHelp: () => throwError(() => ({ error: { message: 'unavailable' } })),
      confirmUpload: () => throwError(() => ({ error: { detail: 'confirm failed' } }))
    });
    expect(failed.component.error).toBe('Dashboard data is unavailable.');
    failed.component.askHelp();
    expect(failed.component.helpLoading).toBeFalse();
    failed.component.helpQuestion = 'question';
    failed.component.askHelp();
    expect(failed.component.helpError).toBe('unavailable');
    failed.component.selectedFile = new File(['x'], 'invalid.csv');
    failed.component.upload();
    expect(failed.component.error).toBe('invalid');
    failed.component.selectedFile = new File(['x'], 'x.csv');
    failed.component.validation = { validationId: 'v', duplicateFile: false } as UploadValidationResult;
    failed.component.decideValidation({ keepNullRows: true, skipDuplicateRows: false, skipInvalidRows: false, renameDuplicate: false, selectedCustomColumns: [], mappings: [], selectedMetadataColumns: [] });
    expect(failed.component.message).toBe('Import started in the background.');
  });
});

describe('ExplorerComponent and HistoryComponent', () => {
  it('loads explorer metrics and selects uploads', () => {
    const ingestion = { history: () => of([upload(1, 'a.csv', 10), upload(2, 'b.csv', 20)]) };
    const component = new ExplorerComponent(ingestion as any);
    expect(component.totalRows).toBe(30);
    expect(component.totalSheets).toBe(4);
    expect(component.totalDuplicates).toBe(2);
    expect(component.maximumRows).toBe(20);
    expect(component.rowWidth(component.uploads[0])).toBe(50);
    component.select(component.uploads[1]);
    expect(component.selected?.uploadId).toBe(2);
  });

  it('filters, sorts, downloads, toggles and clears history', () => {
    const items = [upload(1, 'z.csv', 5), { ...upload(2, 'a.csv', 20), isActive: false, uploadedOn: '2026-08-17T10:00:00Z' }];
    const ingestion = { history: () => of(items), download: () => of(new Blob(['x'])), setStatus: jasmine.createSpy('setStatus').and.returnValue(of({})) };
    const auth = { role: () => 'Customer' };
    const component = new HistoryComponent(ingestion as any, auth as any);
    component.search = 'a';
    expect(component.filtered[0].originalFileName).toBe('a.csv');
    component.search = '';
    component.statusFilter = 'active';
    expect(component.filtered.length).toBe(1);
    component.sortBy = 'rows';
    expect(component.filtered[0].totalRows).toBe(5);
    component.download(items[0]);
    expect(component.downloadingId).toBeUndefined();
    component.toggle(items[0]);
    expect(ingestion.setStatus).toHaveBeenCalledWith(1, false);
    component.clearFilters();
    expect(component.search).toBe('');
    expect(component.statusFilter).toBe('all');
    expect(component.sortBy).toBe('newest');
  });
});

describe('LoginComponent and upload dialog', () => {
  it('submits login/registration and displays backend errors', () => {
    const auth = { login: jasmine.createSpy('login').and.returnValue(of({})), register: jasmine.createSpy('register').and.returnValue(of({})) };
    const router = { navigate: jasmine.createSpy('navigate') };
    const component = new LoginComponent(auth as any, router as any);
    component.userName = 'user'; component.password = 'password'; component.submit();
    expect(auth.login).toHaveBeenCalledWith('user', 'password');
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
    component.registerMode = true; component.submit();
    expect(auth.register).toHaveBeenCalled();
    auth.login.and.returnValue(throwError(() => ({ error: { message: 'bad login' } })));
    component.registerMode = false; component.submit();
    expect(component.error).toBe('bad login');
  });

  it('emits dialog decisions and blocks them while processing', () => {
    const component = new UploadValidationDialogComponent();
    spyOn(component.decision, 'emit');
    component.processing = true;
    component.close(); component.confirm();
    expect(component.decision.emit).not.toHaveBeenCalled();
    component.processing = false;
    component.close();
    expect(component.decision.emit).toHaveBeenCalledWith(null);
    component.result = { duplicateFile: true } as UploadValidationResult;
    component.keepNullRows = false; component.skipDuplicateRows = true; component.skipInvalidRows = true;
    component.confirm();
    expect(component.decision.emit).toHaveBeenCalledWith({ keepNullRows: false, skipDuplicateRows: true, skipInvalidRows: true, renameDuplicate: true, selectedCustomColumns: [], mappings: [], selectedMetadataColumns: [] });
  });
});

describe('ViewerComponent', () => {
  function createViewer(overrides: any = {}) {
    const sheet: Sheet = { sheetId: 10, sheetName: 'Orders', sheetOrder: 0, totalRows: 2, totalColumns: 3 };
    const ingestion = { sheets: () => of([sheet]), standardCoverage: () => of([]), columns: () => of(columns), rows: () => of(rows), analyzeSchema: () => of({ uploadId: 1, fileName: 'x', model: 'gemini', metadataOnly: true, sheetsAnalyzed: 1, columnsAnalyzed: 3, analysis: 'SHEET ANALYSIS: Orders\nClear order data.', generatedOn: '' }), askAi: () => of({ answer: 'answer' }), ...overrides };
    const route = { snapshot: { paramMap: { get: () => '1' } } };
    return { component: new ViewerComponent(route as any, ingestion as any), ingestion, sheet };
  }

  it('loads sheets, columns and rows and computes visual analysis', () => {
    const { component } = createViewer();
    expect(component.selected?.sheetName).toBe('Orders');
    expect(component.columns.length).toBe(3);
    expect(component.rows.length).toBe(2);
    expect(component.columnAnalysis[0].filled).toBe(2);
    expect(component.rowPoints.length).toBe(2);
    expect(component.rowLinePoints).toContain(',');
    expect(component.firstRowNumber).toBe(2);
    expect(component.lastRowNumber).toBe(3);
    expect(component.rowComparisonLabel).toBe('Numeric total per row');
    expect(component.rowAverage).toBe(15);
    expect(component.rowMinimum).toBe(10);
    expect(component.rowMaximum).toBe(20);
    expect(component.valueColumns.length).toBe(1);
    component.selectedValueColumnId = 2;
    component.selectedCategoryColumnId = 1;
    expect(component.barPoints[0].value).toBe(20);
    expect(component.selectedCategoryName).toBe('Facility');
    expect(component.selectedValueName).toBe('Quantity');
    component.toggleColumn(columns[0]);
    expect(component.visibleColumns.length).toBe(2);
    component.nextPage();
    component.previousPage();
  });

  it('supports AI analysis/chat, search and pagination guards', () => {
    const { component } = createViewer();
    component.analyzeWithAi();
    expect(component.aiAnalysis?.analysis).toBe('Clear order data.');
    component.aiQuestion = 'What is quantity?';
    component.askAi();
    expect(component.aiChatAnswer).toBe('answer');
    component.chatOpen = true; component.closeChat();
    expect(component.chatOpen).toBeFalse();
    component.hasNextPage = true; component.nextPage();
    expect(component.page).toBe(2);
    component.search = 'North'; component.searchRows();
    expect(component.page).toBe(1);
  });

  it('handles AI and row-loading failures', () => {
    const viewer = createViewer({ analyzeSchema: () => throwError(() => ({ error: { message: 'AI failed' } })), askAi: () => throwError(() => ({ error: { message: 'chat failed' } })), rows: () => throwError(() => new Error('rows failed')) });
    viewer.component.analyzeWithAi();
    expect(viewer.component.aiError).toBe('AI failed');
    viewer.component.aiQuestion = 'question'; viewer.component.askAi();
    expect(viewer.component.aiChatError).toBe('chat failed');
    expect(viewer.component.loading).toBeFalse();
  });
});
