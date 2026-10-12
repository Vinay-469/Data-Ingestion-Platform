import { Pipe, PipeTransform } from '@angular/core';
import { Cell } from './models';
@Pipe({ name: 'cellValue', standalone: true })
export class CellValuePipe implements PipeTransform { transform(cells: Cell[], columnId: number): string { const value = cells.find(cell => cell.columnId === columnId)?.value?.trim(); return value || '—'; } }
