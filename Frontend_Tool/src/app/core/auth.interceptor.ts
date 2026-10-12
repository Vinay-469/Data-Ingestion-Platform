import { HttpContextToken, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, finalize, throwError } from 'rxjs';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';
import { LoadingService } from './loading.service';
export const SKIP_GLOBAL_LOADING = new HttpContextToken<boolean>(() => false);
export const authInterceptor: HttpInterceptorFn = (request, next) => { const auth = inject(AuthService); const loading = inject(LoadingService); const token = auth.token(); const isAiRequest = request.url.includes('/api/ai/'); const isUploadRequest = (request.method === 'POST' && /\/api\/uploads(?:\/validate|\/confirm|\/confirm-async)?$/.test(request.url)) || request.url.includes('/api/uploads/jobs/'); const skipLoading = request.context.get(SKIP_GLOBAL_LOADING); if (!skipLoading && !isUploadRequest) loading.start(isAiRequest, false); return next(token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request).pipe(catchError(error => { if (error instanceof HttpErrorResponse && error.status === 401 && !request.url.includes('/auth/')) { auth.clearSession(); } return throwError(() => error); }), finalize(() => { if (!skipLoading && !isUploadRequest) loading.stop(isAiRequest, false); })); };
