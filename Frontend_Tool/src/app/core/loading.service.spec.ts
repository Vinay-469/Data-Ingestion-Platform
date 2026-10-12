import { fakeAsync, tick } from '@angular/core/testing';
import { LoadingService } from './loading.service';

describe('LoadingService', () => {
  it('tracks upload progress and stops after completion', fakeAsync(() => {
    const service = new LoadingService();

    service.start(false, true);
    expect(service.isVisible()).toBeTrue();
    expect(service.isUploadVisible()).toBeTrue();
    expect(service.uploadProgress()).toBe(0);
    service.setUploadProgress(74.6);
    expect(service.uploadProgress()).toBe(75);

    service.stop(false, true);
    expect(service.isVisible()).toBeFalse();
    expect(service.isUploadVisible()).toBeFalse();
    expect(service.uploadProgress()).toBe(0);
  }));

  it('tracks AI requests and overlapping requests independently', () => {
    const service = new LoadingService();
    service.start(true, false);
    service.start(true, true);
    expect(service.isVisible()).toBeTrue();
    expect(service.isAiVisible()).toBeTrue();
    expect(service.isUploadVisible()).toBeTrue();
    service.stop(true, false);
    expect(service.isAiVisible()).toBeTrue();
    expect(service.isVisible()).toBeTrue();
    service.stop(true, true);
    expect(service.isAiVisible()).toBeFalse();
    expect(service.isVisible()).toBeFalse();
    service.stop(false, false);
    expect(service.isVisible()).toBeFalse();
  });

  it('supports default regular request arguments', () => {
    const service = new LoadingService();
    service.start();
    expect(service.isVisible()).toBeTrue();
    service.stop();
    expect(service.isVisible()).toBeFalse();
  });
});
