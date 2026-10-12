import { Component } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { IngestionService } from '../../core/ingestion.service';
import { AiSchemaAnalysis, Column, Row, Sheet, StandardColumnCoverage } from '../../core/models';
import { CellValuePipe } from '../../core/cell-value.pipe';
import { FormsModule } from '@angular/forms';

interface ChartItem { label: string; value: number; percent: number; }
interface ColumnAnalysis { column: Column; filled: number; blank: number; distinct: number; fillPercent: number; }
interface RowPoint { rowNumber: number; value: number; x: number; y: number; }
interface BarPoint { label: string; value: number; percent: number; }

@Component({ selector: 'app-viewer', standalone: true, imports: [RouterLink, CellValuePipe, FormsModule, DecimalPipe], templateUrl: './viewer.component.html', styleUrls: ['./viewer.component.css', './viewer-coverage-overrides.css'] })
export class ViewerComponent {
  sheets: Sheet[] = [];
  columns: Column[] = [];
  rows: Row[] = [];
  selected?: Sheet;
  search = '';
  page = 1;
  pageSize = 20;
  hasNextPage = false;
  loading = false;
  sheetChanging = false;
  chartLoading = true;
  sheetRows: ChartItem[] = [];
  dataTypes: ChartItem[] = [];
  uniqueProfiles: ChartItem[] = [];
  currentPageValid = 0;
  currentPageFlagged = 0;
  selectedCategoryColumnId?: number;
  selectedValueColumnId?: number;
  aiAnalysis?: AiSchemaAnalysis;
  private workbookAiAnalysis?: AiSchemaAnalysis;
  aiLoading = false;
  aiError = '';
  aiQuestion = '';
  aiChatAnswer = '';
  aiChatLoading = false;
  aiChatError = '';
  chatOpen = false;
  standardColumnCoverage: StandardColumnCoverage[] = [];

  get sheetAiAnalysis() {
    const report = this.aiAnalysis?.analysis ?? '';
    const sheetName = this.selected?.sheetName;
    if (!report || !sheetName) return report;
    const escaped = sheetName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const section = report.match(new RegExp(`SHEET ANALYSIS:\\s*${escaped}\\s*([\\s\\S]*?)(?=\\nSHEET ANALYSIS:|$)`, 'i'));
    return section?.[1]?.trim() || report;
  }

  constructor(private readonly route: ActivatedRoute, readonly ingestion: IngestionService) {
    const id = Number(this.route.snapshot.paramMap.get('uploadId'));
    this.ingestion.sheets(id).subscribe(sheets => {
      this.sheets = sheets;
      this.sheetRows = this.toChartItems(sheets.map(sheet => ({ label: sheet.sheetName, value: sheet.totalRows })));
      this.loadWorkbookCharts(sheets);
      if (sheets[0]) this.select(sheets[0]);
    });
    this.ingestion.standardCoverage(id).subscribe({ next: coverage => this.standardColumnCoverage = coverage, error: () => this.standardColumnCoverage = [] });
  }

  coverageTotal() { return this.standardColumnCoverage.reduce((total, item) => total + item.filled, 0); }
  coveragePercent(item: StandardColumnCoverage) { const total = item.filled + item.blank; return total ? item.filled / total * 100 : 0; }
  coveragePieGradient() { const colors = ['#30afff', '#1688c7', '#126b91', '#65d7ff', '#1c6fa3', '#8ee7ff']; const total = this.coverageTotal(); if (!total) return 'conic-gradient(var(--line) 0 100%)'; let cursor = 0; const stops = this.standardColumnCoverage.map((item, index) => { const start = cursor; cursor += item.filled / total * 100; return `${colors[index % colors.length]} ${start}% ${cursor}%`; }); return `conic-gradient(${stops.join(', ')})`; }

  select(sheet: Sheet) { this.selected = sheet; this.showSheetAnalysis(); this.page = 1; this.search = ''; this.hasNextPage = false; this.selectedCategoryColumnId = undefined; this.selectedValueColumnId = undefined; this.loading = true; this.sheetChanging = true; this.ingestion.columns(sheet.sheetId).subscribe({ next: columns => { this.columns = columns; this.loadRows(true); }, error: () => { this.columns = []; this.rows = []; this.loading = false; this.sheetChanging = false; } }); }
  analyzeWithAi() { const id = Number(this.route.snapshot.paramMap.get('uploadId')); if (!id || this.aiLoading) return; this.aiLoading = true; this.aiError = ''; this.ingestion.analyzeSchema(id).subscribe({ next: result => { this.workbookAiAnalysis = result; this.showSheetAnalysis(); this.aiLoading = false; }, error: error => { this.aiError = error.error?.message ?? 'AI analysis could not be completed.'; this.aiLoading = false; } }); }
  private showSheetAnalysis() { if (!this.workbookAiAnalysis || !this.selected) return; const escaped = this.selected.sheetName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'); const section = this.workbookAiAnalysis.analysis.match(new RegExp(`SHEET ANALYSIS:\\s*${escaped}\\s*([\\s\\S]*?)(?=\\nSHEET ANALYSIS:|$)`, 'i')); this.aiAnalysis = { ...this.workbookAiAnalysis, analysis: section?.[1]?.trim() || this.workbookAiAnalysis.analysis }; }
  askAi() { const id = Number(this.route.snapshot.paramMap.get('uploadId')); const question = this.aiQuestion.trim(); if (!id || !question || this.aiChatLoading) return; this.aiChatLoading = true; this.aiChatError = ''; this.ingestion.askAi(id, question).subscribe({ next: result => { this.aiChatAnswer = result.answer; this.aiChatLoading = false; }, error: error => { this.aiChatError = error.error?.message ?? 'The workbook assistant could not answer this question.'; this.aiChatLoading = false; } }); }
  closeChat() { this.chatOpen = false; }
  loadRows(force = false) { if (!this.selected || (this.loading && !force)) return; this.loading = true; this.ingestion.rows(this.selected.sheetId, this.page, this.pageSize, this.search).subscribe({ next: rows => { this.rows = rows; this.hasNextPage = this.search.trim().length > 0 ? rows.length === this.pageSize : this.page * this.pageSize < this.selected!.totalRows; this.currentPageValid = rows.filter(row => row.isValid).length; this.currentPageFlagged = rows.length - this.currentPageValid; this.selectedValueColumnId ??= this.valueColumns[0]?.columnId; this.selectedCategoryColumnId ??= this.categoryColumns.find(column => column.columnId !== this.selectedValueColumnId)?.columnId; this.loading = false; this.sheetChanging = false; }, error: () => { this.hasNextPage = false; this.loading = false; this.sheetChanging = false; } }); }
  searchRows() { this.page = 1; this.loadRows(); }
  private loadWorkbookCharts(sheets: Sheet[]) { if (!sheets.length) { this.chartLoading = false; return; } this.chartLoading = true; forkJoin(sheets.map(sheet => this.ingestion.columns(sheet.sheetId))).subscribe({ next: columnsBySheet => { const columns = columnsBySheet.flat(); const types = new Map<string, number>(); const unique = new Map<string, number>(); for (const column of columns) { const type = column.detectedDataType || 'unknown'; types.set(type, (types.get(type) ?? 0) + 1); unique.set(column.columnName || `Column ${column.columnOrder + 1}`, column.uniqueValues); } this.dataTypes = this.toChartItems([...types.entries()].map(([label, value]) => ({ label, value }))); this.uniqueProfiles = this.toChartItems([...unique.entries()].sort((a, b) => b[1] - a[1]).slice(0, 8).map(([label, value]) => ({ label, value }))); this.chartLoading = false; }, error: () => this.chartLoading = false }); }
  private toChartItems(items: { label: string; value: number }[]): ChartItem[] { const maximum = Math.max(...items.map(item => item.value), 1); return items.map(item => ({ ...item, percent: item.value / maximum * 100 })); }
  get columnAnalysis(): ColumnAnalysis[] { const total = this.rows.length || 1; return this.columns.map(column => { const values = this.rows.map(row => row.cells.find(cell => cell.columnId === column.columnId)?.value ?? '').map(value => value.trim()); const filled = values.filter(Boolean).length; return { column, filled, blank: values.length - filled, distinct: new Set(values.filter(Boolean)).size, fillPercent: filled / total * 100 }; }); }
  get rowPoints(): RowPoint[] { const numericColumns = this.columns.filter(column => column.detectedDataType.toLowerCase() === 'number'); const hasAnyValue = this.rows.some(row => row.cells.some(cell => !!cell.value?.trim())); if (!hasAnyValue) return []; const values = this.rows.map(row => { const cells = row.cells.filter(cell => !numericColumns.length || numericColumns.some(column => column.columnId === cell.columnId)); const numericValues = cells.map(cell => Number(cell.value)).filter(value => Number.isFinite(value)); return numericValues.length ? numericValues.reduce((sum, value) => sum + value, 0) : cells.filter(cell => !!cell.value?.trim()).length; }); const maximum = Math.max(...values, 1); const minimum = Math.min(...values, 0); const range = Math.max(maximum - minimum, 1); return values.map((value, index) => ({ rowNumber: this.rows[index].rowNumber, value, x: this.rows.length > 1 ? index / (this.rows.length - 1) * 1000 : 500, y: 190 - (value - minimum) / range * 155 })); }
  get rowLinePoints() { return this.rowPoints.map(point => `${point.x},${point.y}`).join(' '); }
  get firstRowNumber() { return this.rowPoints.length ? this.rowPoints[0].rowNumber : '—'; }
  get lastRowNumber() { return this.rowPoints.length ? this.rowPoints[this.rowPoints.length - 1].rowNumber : '—'; }
  get firstDataTypePercent() { return this.dataTypes.length ? this.dataTypes[0].percent : 0; }
  get secondDataTypePercent() { return this.dataTypes.length > 1 ? this.dataTypes[1].percent : 0; }
  get rowComparisonLabel() { return this.columns.some(column => column.detectedDataType.toLowerCase() === 'number') ? 'Numeric total per row' : 'Populated values per row'; }
  get rowAverage() { const points = this.rowPoints; return points.length ? points.reduce((sum, point) => sum + point.value, 0) / points.length : 0; }
  get rowMinimum() { return this.rowPoints.length ? Math.min(...this.rowPoints.map(point => point.value)) : 0; }
  get rowMaximum() { return this.rowPoints.length ? Math.max(...this.rowPoints.map(point => point.value)) : 0; }
  get categoryColumns() { return this.columns.filter(column => column.columnId !== this.selectedValueColumnId); }
  get valueColumns() { return this.columns.filter(column => this.rows.some(row => { const value = row.cells.find(cell => cell.columnId === column.columnId)?.value?.replaceAll(',', '').trim(); return value !== '' && Number.isFinite(Number(value)); })); }
  get barPoints(): BarPoint[] { if (this.selectedCategoryColumnId === undefined || this.selectedValueColumnId === undefined) return []; const groups = new Map<string, number>(); for (const row of this.rows) { const category = row.cells.find(cell => cell.columnId === this.selectedCategoryColumnId)?.value?.trim(); const valueText = row.cells.find(cell => cell.columnId === this.selectedValueColumnId)?.value?.replaceAll(',', '').trim(); const value = Number(valueText); if (!category || !Number.isFinite(value)) continue; groups.set(category, (groups.get(category) ?? 0) + value); } const points = [...groups.entries()].sort((a, b) => b[1] - a[1]).slice(0, 12); const maximum = Math.max(...points.map(point => point[1]), 1); return points.map(([label, value]) => ({ label, value, percent: value / maximum * 100 })); }
  get selectedCategoryName() { return this.columns.find(column => column.columnId === this.selectedCategoryColumnId)?.displayName || this.columns.find(column => column.columnId === this.selectedCategoryColumnId)?.columnName || 'Category'; }
  get selectedValueName() { return this.columns.find(column => column.columnId === this.selectedValueColumnId)?.displayName || this.columns.find(column => column.columnId === this.selectedValueColumnId)?.columnName || 'Value'; }
  toggleColumn(column: Column) { column.visible = column.visible === false ? true : false; }
  get visibleColumns() { return this.columns.filter(column => column.visible !== false); }
  previousPage() { if (this.page > 1 && !this.loading) { this.page--; this.loadRows(); } }
  nextPage() { if (this.hasNextPage && !this.loading) { this.page++; this.loadRows(); } }
}
