import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminAuditLogSearchResponse } from '../admin/admin-audit-log.models';
import { AdminAuditLogService } from '../admin/admin-audit-log.service';
import { AdminAuditLogsPageComponent } from './admin-audit-logs-page.component';

describe('AdminAuditLogsPageComponent', () => {
  let fixture: ComponentFixture<AdminAuditLogsPageComponent>;
  let auditLogService: jasmine.SpyObj<AdminAuditLogService>;

  beforeEach(async () => {
    auditLogService = jasmine.createSpyObj<AdminAuditLogService>('AdminAuditLogService', ['search']);
    auditLogService.search.and.resolveTo(createSearchResponse());

    await TestBed.configureTestingModule({
      imports: [AdminAuditLogsPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminAuditLogService, useValue: auditLogService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminAuditLogsPageComponent);
  });

  it('loads and displays audit logs', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('ProductApproved');
    expect(compiled.textContent).toContain('Product');
    expect(compiled.textContent).toContain('Manual review complete.');
    expect(compiled.textContent).toContain('1 audit log');
  });

  it('submits filters to the audit log service', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const actionInput = (fixture.nativeElement as HTMLElement).querySelector('input[formControlName="actionType"]') as HTMLInputElement;
    actionInput.value = 'SellerApproved';
    actionInput.dispatchEvent(new Event('input'));

    const form = (fixture.nativeElement as HTMLElement).querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));

    await fixture.whenStable();

    expect(auditLogService.search).toHaveBeenCalledWith(jasmine.objectContaining({
      actionType: 'SellerApproved',
      pageSize: 50
    }));
  });

  it('clears filters and reloads audit logs', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const actionInput = (fixture.nativeElement as HTMLElement).querySelector('input[formControlName="actionType"]') as HTMLInputElement;
    actionInput.value = 'SellerApproved';
    actionInput.dispatchEvent(new Event('input'));

    const clearButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(button => button.textContent?.includes('Clear'));
    clearButton?.dispatchEvent(new Event('click'));

    await fixture.whenStable();

    expect(auditLogService.search.calls.mostRecent().args[0]).toEqual(jasmine.objectContaining({
      actionType: '',
      entityType: '',
      entityId: '',
      actorUserId: '',
      pageSize: 50
    }));
  });
});

function createSearchResponse(): AdminAuditLogSearchResponse {
  return {
    items: [{
      id: 'audit-id',
      actorUserId: 'admin-id',
      actorRole: 'Admin',
      actionType: 'ProductApproved',
      entityType: 'Product',
      entityId: 'product-id',
      previousValueJson: '{"status":"PendingReview"}',
      newValueJson: '{"status":"Published"}',
      reason: 'Manual review complete.',
      ipAddress: '127.0.0.1',
      createdAtUtc: '2026-05-18T12:00:00Z'
    }],
    pageNumber: 1,
    pageSize: 50,
    totalCount: 1
  };
}
