import { Component, EventEmitter, Input, Output, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ColumnMapping, ColumnMappingResult, ConfirmedColumnMapping, MetadataColumnSelection, SourceColumn, UploadValidationResult } from '../../core/models';

interface MappingSheet { sheetName: string; sheetOrder: number; sources: SourceColumn[]; }

@Component({ selector: 'app-upload-validation-dialog', standalone: true, imports: [CommonModule, FormsModule], templateUrl: './upload-validation-dialog.component.html', styleUrls: ['./upload-validation-dialog.component.css', './validation-enterprise-overrides.css'] })
export class UploadValidationDialogComponent implements OnChanges {
  @Input() result?: UploadValidationResult;
  @Input() processing = false;
  @Input() mapping?: ColumnMappingResult;
  @Output() decision = new EventEmitter<{ keepNullRows: boolean; skipDuplicateRows: boolean; skipInvalidRows: boolean; renameDuplicate: boolean; selectedCustomColumns: string[]; mappings: ConfirmedColumnMapping[]; selectedMetadataColumns: MetadataColumnSelection[]; excludedSheetOrders?: number[] } | null>();
  keepNullRows = true; skipDuplicateRows = false; skipInvalidRows = false; selectedCustomColumns: string[] = [];
  mappingSelections: ConfirmedColumnMapping[] = []; selectedMetadataSelections: MetadataColumnSelection[] = []; sourceSearch = ''; manualSearches: Record<string, string> = {}; openManualDropdown: string | null = null; excludedSheetOrders = new Set<number>(); activeSheetOrder = 0; mappingWarning = '';

  ngOnChanges() {
    this.selectedCustomColumns = [...(this.result?.additionalColumns ?? [])];
    this.selectedMetadataSelections = []; this.mappingWarning = ''; this.manualSearches = {}; this.openManualDropdown = null; this.excludedSheetOrders = new Set<number>();
    if (!this.mapping) { this.mappingSelections = []; return; }
    this.activeSheetOrder = this.mappingSheets()[0]?.sheetOrder ?? 0;
    this.mappingSelections = this.mappingSheets().flatMap(sheet => this.mapping!.targetColumns.map(target => {
      const suggestion = this.bestSuggestion(target, sheet.sheetOrder);
      return { sheetName: sheet.sheetName, sheetOrder: sheet.sheetOrder, sourceColumnName: suggestion?.sourceColumnName ?? '', columnOrder: suggestion?.columnOrder ?? -1, targetColumn: target, mappingMethod: suggestion?.mappingMethod ?? 'Manual', confidenceScore: suggestion?.confidenceScore ?? 0, aiReason: suggestion?.reason ?? null };
    }));
  }

  mappingSheets(): MappingSheet[] {
    const grouped = new Map<number, MappingSheet>();
    for (const source of this.mapping?.sourceColumns ?? []) { const sheet = grouped.get(source.sheetOrder) ?? { sheetName: source.sheetName, sheetOrder: source.sheetOrder, sources: [] }; sheet.sources.push(source); grouped.set(source.sheetOrder, sheet); }
    return [...grouped.values()].sort((a, b) => a.sheetOrder - b.sheetOrder).map(sheet => ({ ...sheet, sources: [...sheet.sources].sort((a, b) => a.columnOrder - b.columnOrder) }));
  }
  currentMappingSheet() { return this.mappingSheets().find(sheet => sheet.sheetOrder === this.activeSheetOrder) ?? this.mappingSheets()[0]; }
  sheetIndex() { const sheets = this.mappingSheets(); return Math.max(0, sheets.findIndex(sheet => sheet.sheetOrder === this.currentMappingSheet()?.sheetOrder)); }
  mappedCount(sheetOrder: number) { return this.mapping?.targetColumns.filter(target => this.isTargetMapped(target, sheetOrder)).length ?? 0; }
  unmappedCount(sheetOrder: number) { return Math.max(0, (this.mapping?.targetColumns.length ?? 0) - this.mappedCount(sheetOrder)); }
  isTargetMapped(target: string, sheetOrder: number) { const selected = this.selectedMapping(target, sheetOrder); return selected?.mappingMethod === 'Null' || (!!selected?.sourceColumnName && selected.columnOrder >= 0); }
  goToSheet(direction: number) { const sheets = this.mappingSheets(); const nextIndex = this.sheetIndex() + direction; if (nextIndex >= 0 && nextIndex < sheets.length) { this.activeSheetOrder = sheets[nextIndex].sheetOrder; this.sourceSearch = ''; } }
  isSheetExcluded(sheetOrder: number) { return this.excludedSheetOrders.has(sheetOrder); }
  toggleSheet(sheetOrder: number, include: boolean) { if (include) this.excludedSheetOrders.delete(sheetOrder); else this.excludedSheetOrders.add(sheetOrder); this.excludedSheetOrders = new Set(this.excludedSheetOrders); if (this.isSheetExcluded(this.activeSheetOrder)) this.activeSheetOrder = this.mappingSheets().find(sheet => !this.isSheetExcluded(sheet.sheetOrder))?.sheetOrder ?? this.activeSheetOrder; }
  toggleCustomColumn(column: string, checked: boolean) { this.selectedCustomColumns = checked ? [...new Set([...this.selectedCustomColumns, column])] : this.selectedCustomColumns.filter(item => item !== column); }
  close() { if (!this.processing) this.decision.emit(null); }
  bestSuggestion(target: string, sheetOrder: number): ColumnMapping | undefined { return this.mapping?.mappings.filter(item => item.sheetOrder === sheetOrder && item.targetColumn?.toLowerCase() === target.toLowerCase()).sort((a, b) => b.confidenceScore - a.confidenceScore)[0]; }
  selectedMapping(target: string, sheetOrder: number) { return this.mappingSelections.find(item => item.targetColumn === target && item.sheetOrder === sheetOrder); }
  manualSearchKey(target: string, sheetOrder: number) { return `${sheetOrder}:${target}`; }
  manualSearchValue(target: string, sheetOrder: number) { return this.manualSearches[this.manualSearchKey(target, sheetOrder)] ?? ''; }
  setManualSearch(target: string, sheetOrder: number, value: string) { this.manualSearches[this.manualSearchKey(target, sheetOrder)] = value; }
  toggleManualDropdown(target: string, sheetOrder: number) { const key = this.manualSearchKey(target, sheetOrder); this.openManualDropdown = this.openManualDropdown === key ? null : key; }
  isManualDropdownOpen(target: string, sheetOrder: number) { return this.openManualDropdown === this.manualSearchKey(target, sheetOrder); }
  availableSources(target: string, sheetOrder: number): SourceColumn[] { const selected = this.selectedMapping(target, sheetOrder); const search = this.manualSearchValue(target, sheetOrder).trim().toLowerCase(); const sources = (this.mappingSheets().find(sheet => sheet.sheetOrder === sheetOrder)?.sources ?? []).filter(item => !search || item.sourceColumnName.toLowerCase().includes(search)).filter(item => !this.mappingSelections.some(current => current.targetColumn !== target && current.sheetOrder === sheetOrder && current.columnOrder === item.columnOrder) || selected?.columnOrder === item.columnOrder); const nullOption: SourceColumn = { sheetName: this.mappingSheets().find(sheet => sheet.sheetOrder === sheetOrder)?.sheetName ?? '', sheetOrder, sourceColumnName: 'NULL - leave this field empty', columnOrder: -1, detectedDataType: '' }; return [nullOption, ...sources]; }
  selectedSourceName(target: string, sheetOrder: number) { const selected = this.selectedMapping(target, sheetOrder); return selected?.mappingMethod === 'Null' ? `${sheetOrder}:-1` : selected && selected.columnOrder >= 0 ? `${sheetOrder}:${selected.columnOrder}` : ''; }
  mappingConfidence(target: string, sheetOrder: number) { const selected = this.selectedMapping(target, sheetOrder); return selected?.mappingMethod !== 'Manual' ? selected?.confidenceScore ?? 0 : 0; }
  mappingConfidenceLevel(target: string, sheetOrder: number) { const selected = this.selectedMapping(target, sheetOrder); return selected?.mappingMethod !== 'Manual' ? this.bestSuggestion(target, sheetOrder)?.confidenceLevel ?? 'Low' : 'Manual'; }
  mappingReason(target: string, sheetOrder: number) { const selected = this.selectedMapping(target, sheetOrder); return selected?.mappingMethod !== 'Manual' ? selected?.aiReason || 'Matched from the source header meaning.' : 'Customer-selected mapping'; }
  isAiSelected(target: string, sheetOrder: number) { return this.selectedMapping(target, sheetOrder)?.mappingMethod === 'AI'; }
  isAutomaticSelected(target: string, sheetOrder: number) { return this.selectedMapping(target, sheetOrder)?.mappingMethod === 'Automatic'; }
  selectedSourceLabel(target: string, sheetOrder: number) { const selected = this.selectedMapping(target, sheetOrder); return selected?.mappingMethod === 'Null' ? 'Customer selected: NULL (leave empty)' : selected?.mappingMethod === 'AI' && selected.sourceColumnName ? `AI selected: ${selected.sourceColumnName}` : selected?.mappingMethod === 'Automatic' && selected.sourceColumnName ? `Recommended: ${selected.sourceColumnName}` : selected?.sourceColumnName ? `Customer selected: ${selected.sourceColumnName}` : 'No source column selected'; }
  confidenceClass(target: string, sheetOrder: number) { const level = this.mappingConfidenceLevel(target, sheetOrder).toLowerCase(); return level === 'high' ? 'confidence-high' : level === 'medium' ? 'confidence-medium' : level === 'low' ? 'confidence-low' : 'confidence-manual'; }
  confidenceLabel(target: string, sheetOrder: number) { const selected = this.selectedMapping(target, sheetOrder); return selected?.mappingMethod !== 'Manual' ? `${this.mappingConfidence(target, sheetOrder)}% ${this.mappingConfidenceLevel(target, sheetOrder)}` : 'Manual selection'; }
  isSourceMapped(source: SourceColumn) { return this.mappingSelections.some(item => item.sheetOrder === source.sheetOrder && item.columnOrder === source.columnOrder); }
  currentSheetMetadataSources() { return (this.mapping?.sourceColumns ?? []).filter(source => source.sheetOrder === this.activeSheetOrder && !this.isSourceMapped(source)); }
  selectSource(target: string, source?: SourceColumn) { if (!source) return; const index = this.mappingSelections.findIndex(item => item.targetColumn === target && item.sheetOrder === source.sheetOrder); const next: ConfirmedColumnMapping = { sheetName: source.sheetName, sheetOrder: source.sheetOrder, sourceColumnName: source.sourceColumnName, columnOrder: source.columnOrder, targetColumn: target, mappingMethod: 'Manual', confidenceScore: 0, aiReason: null }; if (index >= 0) this.mappingSelections[index] = next; else this.mappingSelections.push(next); this.mappingSelections = [...this.mappingSelections]; this.mappingWarning = ''; }
  selectSourceByKey(target: string, sheetOrder: number, key: string) { const [selectedSheet, columnOrder] = key.split(':').map(Number); if (selectedSheet === sheetOrder && columnOrder === -1) { const sheet = this.mappingSheets().find(item => item.sheetOrder === sheetOrder); if (!sheet) return; const index = this.mappingSelections.findIndex(item => item.targetColumn === target && item.sheetOrder === sheetOrder); const next: ConfirmedColumnMapping = { sheetName: sheet.sheetName, sheetOrder, sourceColumnName: '', columnOrder: -1, targetColumn: target, mappingMethod: 'Null', confidenceScore: 0, aiReason: 'Customer chose to leave this standard field empty.' }; if (index >= 0) this.mappingSelections[index] = next; else this.mappingSelections.push(next); this.mappingSelections = [...this.mappingSelections]; this.mappingWarning = ''; this.openManualDropdown = null; return; } const source = this.mappingSheets().find(sheet => sheet.sheetOrder === selectedSheet)?.sources.find(item => item.columnOrder === columnOrder); this.selectSource(target, source); this.openManualDropdown = null; }
  toggleMetadata(column: SourceColumn, checked: boolean) { const key = `${column.sheetOrder}:${column.columnOrder}`; this.selectedMetadataSelections = checked ? [...this.selectedMetadataSelections.filter(item => `${item.sheetOrder}:${item.columnOrder}` !== key), { sheetName: column.sheetName, sheetOrder: column.sheetOrder, sourceColumnName: column.sourceColumnName, columnOrder: column.columnOrder }] : this.selectedMetadataSelections.filter(item => `${item.sheetOrder}:${item.columnOrder}` !== key); }
  isMetadataSelected(column: SourceColumn) { return this.selectedMetadataSelections.some(item => item.sheetOrder === column.sheetOrder && item.columnOrder === column.columnOrder); }
  confirm() {
    if (this.processing) return;
    if (!this.mappingSheets().some(sheet => !this.isSheetExcluded(sheet.sheetOrder))) {
      this.mappingWarning = 'Select at least one sheet before importing the workbook.';
      return;
    }
    const incompleteSheet = this.mappingSheets().find(sheet => !this.isSheetExcluded(sheet.sheetOrder) && this.unmappedCount(sheet.sheetOrder) > 0);
    if (incompleteSheet) {
      this.activeSheetOrder = incompleteSheet.sheetOrder;
      this.sourceSearch = '';
      this.mappingWarning = `Complete all required mappings before importing. ${this.unmappedCount(incompleteSheet.sheetOrder)} field(s) still need attention in ${incompleteSheet.sheetName}.`;
      setTimeout(() => document.querySelector('.mapping-row-unmapped select')?.scrollIntoView({ behavior: 'smooth', block: 'center' }), 0);
      return;
    }
    this.mappingWarning = '';
    const canonicalMappings = this.mappingSelections.map(mapping => {
      const sheet = this.mappingSheets().find(item => item.sheetOrder === mapping.sheetOrder);
      const source = sheet?.sources.find(item => item.columnOrder === mapping.columnOrder);
      const target = this.mapping?.targetColumns.find(item => item.toLowerCase() === mapping.targetColumn.toLowerCase()) ?? mapping.targetColumn;
      return { ...mapping, sheetName: source?.sheetName ?? sheet?.sheetName ?? mapping.sheetName, sourceColumnName: source?.sourceColumnName ?? mapping.sourceColumnName, targetColumn: target };
    });
    this.decision.emit({ keepNullRows: this.keepNullRows, skipDuplicateRows: this.skipDuplicateRows, skipInvalidRows: this.skipInvalidRows, renameDuplicate: !!this.result?.duplicateFile, selectedCustomColumns: this.selectedCustomColumns, mappings: canonicalMappings, selectedMetadataColumns: this.selectedMetadataSelections, excludedSheetOrders: [...this.excludedSheetOrders] });
  }
}
