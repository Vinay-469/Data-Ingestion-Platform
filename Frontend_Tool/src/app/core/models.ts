export interface LoginResponse { accessToken: string; userName: string; role: string; expiresAt: string; }
export interface UploadSummary { uploadId: number; userName: string; originalFileName: string; status: string; isActive: boolean; uploadedOn: string; totalSheets: number; totalRows: number; duplicateRows: number; nullRows: number; }
export interface Sheet { sheetId: number; sheetName: string; sheetOrder: number; totalRows: number; totalColumns: number; }
export interface Column { columnId: number; columnName: string; displayName: string; columnOrder: number; detectedDataType: string; allowNull: boolean; uniqueValues: number; visible?: boolean; }
export interface AiSchemaAnalysis { uploadId: number; fileName: string; model: string; metadataOnly: boolean; sheetsAnalyzed: number; columnsAnalyzed: number; analysis: string; generatedOn: string; }
export interface Row { rowId: number; rowNumber: number; hasDuplicate: boolean; hasNull: boolean; isValid: boolean; facility?: string; manufacture?: string; product?: string; productDescription?: string; customerUom?: string; customerQty?: number; cells: Cell[]; }
export interface Cell { columnId: number; columnName: string; value?: string; detectedDataType: string; validationStatus: string; }
export interface DailyUploadPoint { date: string; count: number; }
export interface DailyUploadSeries { label: string; points: DailyUploadPoint[]; }
export interface Dashboard { totalUploads: number; filesUploaded: number; rowsImported: number; sheetsImported: number; duplicateRows: number; nullRows: number; successRate: number; failureRate: number; uploadTrendPercent?: number | null; rowsTrendPercent?: number | null; dailyUploads: DailyUploadSeries[]; }
export interface StandardColumnCoverage { columnName: string; filled: number; blank: number; }
export interface AdminCustomerVolume { userName: string; uploads: number; rows: number; }
export interface AdminDashboard extends Dashboard { totalUsers: number; activeUploads: number; inactiveUploads: number; recentUploads: UploadSummary[]; standardColumnCoverage?: StandardColumnCoverage[]; customerVolume?: AdminCustomerVolume[]; }
export interface SourceColumn { sheetName: string; sheetOrder: number; sourceColumnName: string; columnOrder: number; detectedDataType: string; }
export interface ColumnMapping { sheetName: string; sheetOrder: number; sourceColumnName: string; columnOrder: number; targetColumn?: string | null; confidenceScore: number; confidenceLevel: string; reason?: string | null; mappingMethod: string; isRecommended: boolean; }
export interface ConfirmedColumnMapping { sheetName: string; sheetOrder: number; sourceColumnName: string; columnOrder: number; targetColumn: string; mappingMethod: string; confidenceScore?: number; aiReason?: string | null; }
export interface MetadataColumnSelection { sheetName: string; sheetOrder: number; sourceColumnName: string; columnOrder: number; }
export interface ColumnMappingResult { validationId: string; sourceColumns: SourceColumn[]; mappings: ColumnMapping[]; targetColumns: string[]; aiAvailable: boolean; message?: string; }
export interface UploadValidationResult { validationId: string; originalFileName: string; status: string; expiresAt: string; totalSheets: number; totalRows: number; totalColumns: number; duplicateRows: number; nullRows: number; duplicateFile: boolean; duplicateSheetNames: number; duplicateColumns: number; emptySheets: number; emptyWorkbook: boolean; invalidDataTypeCells: number; issues: string[]; additionalColumns: string[]; sourceColumns?: SourceColumn[]; }
export interface ValidationSummary { uploadId: number; status: string; totalRows: number; duplicateRows: number; nullRows: number; skippedRows: number; totalSheets: number; errorMessage?: string; }
export interface UploadJobStatus { jobId: string; status: 'Processing' | 'Completed' | 'Failed' | 'Cancelled'; stage: 'Importing' | 'Completed' | 'Failed' | 'Cancelled'; progress: number; result?: ValidationSummary; errorMessage?: string; queuedAt: string; startedAt?: string; completedAt?: string; elapsedMilliseconds?: number; processingMilliseconds?: number; storageMilliseconds?: number; }
