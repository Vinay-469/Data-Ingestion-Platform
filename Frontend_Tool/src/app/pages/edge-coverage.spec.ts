import { of, throwError } from 'rxjs';
import { DashboardComponent } from './dashboard/dashboard.component';
import { HistoryComponent } from './history/history.component';
import { ExplorerComponent } from './explorer/explorer.component';
import { ViewerComponent } from './viewer/viewer.component';
import { LoginComponent } from './login/login.component';
import { UploadValidationResult } from '../core/models';

const emptyValidation = { validationId: 'v', duplicateFile: false } as UploadValidationResult;

describe('frontend edge branches', () => {
  it('covers dashboard greeting time windows and empty/locked help/upload paths', () => {
    const ingestion: any = { dashboard: () => of({}), history: () => of([]), adminDashboard: () => of({}), askHelp: () => of({ answer: 'ok' }), validateUpload: () => of(emptyValidation), mapColumns: () => of({ validationId: 'v', sourceColumns: [], mappings: [], targetColumns: [], aiAvailable: true }), cancelValidation: () => of({}), confirmUpload: () => of({ skippedRows: 2 }) };
    const progress: any = { completed: of(undefined), startUpload: () => undefined, reviewReady: () => undefined, uploadFailed: () => undefined, startImport: () => undefined };
    const component = new DashboardComponent({ role: () => 'Customer' } as any, ingestion, {} as any, {} as any, progress, {} as any);
    jasmine.clock().install();
    const hours = [8, 13, 18, 23];
    for (const hour of hours) {
      jasmine.clock().mockDate(new Date(2026, 0, 1, hour));
      expect(['Good morning', 'Good afternoon', 'Good evening', 'Good night']).toContain(component.greeting);
    }
    jasmine.clock().uninstall();
    component.askHelp();
    component.helpLoading = true; component.helpQuestion = 'question'; component.askHelp();
    component.selectedFile = undefined; component.upload();
    component.validation = emptyValidation; component.decideValidation(null);
    expect(component.validation).toBeUndefined();
  });

  it('covers dashboard fallback errors, skipped rows, renamed validation failure and download error', () => {
    const ingestion: any = {
      dashboard: () => of({}), history: () => of([]), validateUpload: jasmine.createSpy('validate').and.returnValues(throwError(() => ({})), throwError(() => ({}))),
      cancelValidation: () => of({}), confirmUpload: () => of({ skippedRows: 2 }), download: () => throwError(() => ({}))
    };
    const progress: any = { completed: of(undefined), startUpload: () => undefined, reviewReady: () => undefined, uploadFailed: () => undefined, startImport: () => undefined };
    const component = new DashboardComponent({ role: () => 'Customer' } as any, ingestion, {} as any, {} as any, progress, {} as any);
    component.selectedFile = new File(['x'], 'data.csv');
    component.upload();
    expect(component.error).toBe('File validation failed.');
    component.validation = { ...emptyValidation, duplicateFile: true };
    component.decideValidation({ keepNullRows: true, skipDuplicateRows: false, skipInvalidRows: false, renameDuplicate: true, selectedCustomColumns: [], mappings: [], selectedMetadataColumns: [] });
    expect(component.error).toBe('Renamed file validation failed.');
    component.validation = emptyValidation;
    component.decideValidation({ keepNullRows: true, skipDuplicateRows: false, skipInvalidRows: false, renameDuplicate: false, selectedCustomColumns: [], mappings: [], selectedMetadataColumns: [] });
    expect(component.message).toBe('Import started in the background.');
    component.downloadUpload({ ...({ uploadId: 1, originalFileName: 'x.csv' } as any) });
    expect(component.error).toBe('File download failed.');
    const successIngestion: any = { dashboard: () => of({}), history: () => throwError(() => new Error('history')), adminDashboard: () => throwError(() => new Error('admin')), download: () => of(new Blob(['x'])) };
    const success = new DashboardComponent({ role: () => 'Admin' } as any, successIngestion, {} as any, {} as any, { completed: of(undefined) } as any, {} as any);
    spyOn(URL, 'createObjectURL').and.returnValue('blob:test');
    spyOn(URL, 'revokeObjectURL');
    spyOn(HTMLAnchorElement.prototype, 'click');
    success.downloadUpload({ uploadId: 2, originalFileName: 'good.csv', isActive: true } as any);
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:test');
    expect(success.dailyTotal(undefined)).toBe(0);
    expect(success.dailyPeak(undefined)).toBe(0);
    expect(success.chartDate(undefined, 0)).toBe('—');
    expect(success.smoothChartPath({ label: 'two', points: [{ date: 'a', count: 1 }, { date: 'b', count: 2 }] }, seriesForTest())).toContain('Q ');
    expect((success as any).withDuplicateSuffix(new File(['x'], 'noextension'), 2).name).toBe('noextension-2');
    const helpFallback = new DashboardComponent({ role: () => 'Customer' } as any, { dashboard: () => of({}), history: () => of([]), askHelp: () => throwError(() => ({})) } as any, {} as any, {} as any, { completed: of(undefined) } as any, {} as any);
    helpFallback.helpQuestion = 'question'; helpFallback.askHelp();
    expect(helpFallback.helpError).toBe('The help assistant is temporarily unavailable.');
  });

  it('covers explorer empty metrics and all history sorting/filter/error guards', () => {
    const explorer = new ExplorerComponent({ history: () => of([]) } as any);
    expect(explorer.maximumRows).toBe(1);
    expect(explorer.rowWidth({ totalRows: 0 } as any)).toBe(0);
    const ingestion: any = { history: () => of([{ uploadId: 1, originalFileName: 'b', totalRows: 1, isActive: false, uploadedOn: '2026-01-01' }, { uploadId: 2, originalFileName: 'a', totalRows: 2, isActive: true, uploadedOn: '2026-01-02' }]), download: () => throwError(() => new Error('download')), setStatus: () => of({}) };
    const history = new HistoryComponent(ingestion, {} as any);
    history.sortBy = 'name'; expect(history.filtered[0].originalFileName).toBe('a');
    history.statusFilter = 'inactive'; expect(history.filtered.length).toBe(1);
    history.downloadingId = 3; history.download(history.uploads[0]); expect(history.downloadingId).toBe(3);
    history.downloadingId = undefined; history.download(history.uploads[0]); expect(history.downloadingId).toBeUndefined();
    history.statusFilter = 'all'; history.sortBy = 'newest'; expect(history.filtered.length).toBe(2);
    history.sortBy = 'rows'; expect(history.filtered[0].totalRows).toBe(2);
  });

  it('covers login fallback registration error and viewer empty/guard branches', () => {
    const login = new LoginComponent({ login: () => throwError(() => ({})), register: () => throwError(() => ({})) } as any, {} as any);
    login.submit(); expect(login.error).toBe('Login failed.');
    login.registerMode = true; login.submit(); expect(login.error).toBe('Registration failed.');
    const sheet = { sheetId: 1, sheetName: 'Empty', sheetOrder: 0, totalRows: 0, totalColumns: 0 };
    const ingestion: any = { sheets: () => of([]), standardCoverage: () => of([]), columns: () => throwError(() => new Error('columns')), rows: () => of([]), analyzeSchema: () => of({ analysis: '', uploadId: 1 }), askAi: () => of({ answer: 'x' }) };
    const viewer = new ViewerComponent({ snapshot: { paramMap: { get: () => '0' } } } as any, ingestion);
    viewer.analyzeWithAi(); viewer.askAi(); viewer.loadRows();
    viewer.selected = sheet as any; viewer.loading = true; viewer.loadRows();
    viewer.selected = undefined; viewer.previousPage(); viewer.nextPage();
    expect(viewer.rowPoints).toEqual([]);
    expect(viewer.barPoints).toEqual([]);
  });

  it('covers viewer workbook analysis fallbacks, chart errors and nonnumeric rows', () => {
    const sheet: any = { sheetId: 1, sheetName: 'Orders', sheetOrder: 0, totalRows: 1, totalColumns: 1 };
    const stringColumn: any = { columnId: 1, columnName: '', displayName: '', columnOrder: 0, detectedDataType: 'string', allowNull: true, uniqueValues: 0 };
    const stringRow: any = { rowId: 1, rowNumber: 1, isValid: true, hasNull: false, hasDuplicate: false, cells: [{ columnId: 1, columnName: 'Text', value: 'hello', detectedDataType: 'string', validationStatus: 'Valid' }] };
    const ingestion: any = { sheets: () => of([sheet]), standardCoverage: () => of([]), columns: () => of([stringColumn]), rows: () => of([stringRow]), analyzeSchema: () => throwError(() => ({})), askAi: () => throwError(() => ({})) };
    const viewer = new ViewerComponent({ snapshot: { paramMap: { get: () => '1' } } } as any, ingestion);
    ingestion.columns = () => throwError(() => new Error('chart error'));
    (viewer as any).loadWorkbookCharts([sheet]);
    expect(viewer.chartLoading).toBeFalse();
    viewer.aiAnalysis = undefined; expect(viewer.sheetAiAnalysis).toBe('');
    viewer.aiAnalysis = { analysis: 'plain analysis' } as any; expect(viewer.sheetAiAnalysis).toBe('plain analysis');
    (viewer as any).workbookAiAnalysis = { analysis: 'plain workbook analysis' }; (viewer as any).showSheetAnalysis();
    expect(viewer.aiAnalysis?.analysis).toBe('plain workbook analysis');
    viewer.analyzeWithAi(); expect(viewer.aiError).toBe('AI analysis could not be completed.');
    viewer.aiQuestion = 'question'; viewer.askAi(); expect(viewer.aiChatError).toBe('The workbook assistant could not answer this question.');
    expect(viewer.rowPoints[0].x).toBe(500);
    expect(viewer.rowComparisonLabel).toBe('Populated values per row');
    expect(viewer.firstDataTypePercent).toBe(100);
    expect(viewer.secondDataTypePercent).toBe(0);
    expect(viewer.rowAverage).toBe(1);
    viewer.selectedCategoryColumnId = 99; viewer.selectedValueColumnId = 98;
    expect(viewer.selectedCategoryName).toBe('Category');
    expect(viewer.selectedValueName).toBe('Value');
    viewer.rows = [{ ...stringRow, cells: [{ ...stringRow.cells[0], value: '' }] }, { ...stringRow, cells: [{ ...stringRow.cells[0], value: 'not-a-number' }] }];
    viewer.selectedCategoryColumnId = 1; viewer.selectedValueColumnId = 1;
    expect(viewer.barPoints).toEqual([]);
    viewer.columns = [{ ...stringColumn, visible: false }]; viewer.toggleColumn(viewer.columns[0]);
    expect(viewer.columns[0].visible).toBeTrue();
    viewer.page = 2; viewer.loading = false; viewer.previousPage(); expect(viewer.page).toBe(1);
    viewer.columns = []; viewer.rows = [];
    expect(viewer.columnAnalysis).toEqual([]);
            expect(viewer.firstRowNumber).toBe('\u2014');
            expect(viewer.lastRowNumber).toBe('\u2014');
    viewer.dataTypes = [];
    expect(viewer.firstDataTypePercent).toBe(0);
    expect(viewer.secondDataTypePercent).toBe(0);
    expect(viewer.rowAverage).toBe(0);
    expect(viewer.rowMinimum).toBe(0);
    expect(viewer.rowMaximum).toBe(0);
    viewer.dataTypes = [{ label: 'a', value: 1, percent: 50 }, { label: 'b', value: 1, percent: 50 }];
    expect(viewer.secondDataTypePercent).toBe(50);
    ingestion.columns = () => of([{ ...stringColumn, detectedDataType: '', columnName: '' }]);
    (viewer as any).loadWorkbookCharts([sheet]);
    expect(viewer.dataTypes[0].label).toBe('unknown');
  });
});

function seriesForTest() {
  return [{ label: 'x', points: [{ date: '2026-01-01', count: 1 }, { date: '2026-01-02', count: 2 }] }];
}
