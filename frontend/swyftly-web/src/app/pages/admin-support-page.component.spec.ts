import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminSupportQueueItemResponse, AdminSupportQueueResponse, AdminSupportSummaryResponse, AdminSupportTicketResponse } from '../admin/admin-support.models';
import { AdminSupportService } from '../admin/admin-support.service';
import { AdminSupportPageComponent } from './admin-support-page.component';

describe('AdminSupportPageComponent', () => {
  let fixture: ComponentFixture<AdminSupportPageComponent>;
  let supportService: jasmine.SpyObj<AdminSupportService>;

  beforeEach(async () => {
    supportService = jasmine.createSpyObj<AdminSupportService>('AdminSupportService', [
      'listQueue',
      'getSummary',
      'claimTicket',
      'getSavedViews',
      'createSavedView',
      'updateSavedView',
      'deleteSavedView',
      'makeDefault',
      'exportQueue',
      'getQualityReport',
      'exportQualityReport'
    ]);
    supportService.listQueue.and.resolveTo(createQueue([createQueueItem()]));
    supportService.getSummary.and.resolveTo(createSummary());
    supportService.getQualityReport.and.resolveTo(createQualityReport());
    supportService.claimTicket.and.resolveTo(createAdminSupportTicket({ assignedSupportUserId: 'support-user-id' }));
    supportService.getSavedViews.and.resolveTo([]);
    supportService.createSavedView.and.resolveTo(createSavedView());
    supportService.updateSavedView.and.resolveTo(createSavedView());
    supportService.makeDefault.and.resolveTo(createSavedView({ isDefault: true }));
    supportService.deleteSavedView.and.resolveTo();
    supportService.exportQueue.and.resolveTo(new Blob(['csv'], { type: 'text/csv' }));
    supportService.exportQualityReport.and.resolveTo(new Blob(['quality'], { type: 'text/csv' }));

    await TestBed.configureTestingModule({
      imports: [AdminSupportPageComponent],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: AdminSupportService, useValue: supportService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminSupportPageComponent);
  });

  it('loads support tickets and links to detail', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-admin-workspace-nav')).not.toBeNull();
    expect(compiled.textContent).toContain('Damaged order');
    expect(compiled.textContent).toContain('OrderIssue');
    expect(compiled.querySelector('a[href="/admin/support/ticket-id"]')).not.toBeNull();
  });

  it('sends filters to the support queue', async () => {
    supportService.listQueue.and.resolveTo(createQueue([
      createQueueItem({ supportTicketId: 'resolved-ticket-id', subject: 'Closed case', status: 'Resolved' })
    ]));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      filtersForm: { setValue(value: { savedViewId: string; savedViewName: string; view: string; search: string; status: string; category: string; assigned: string; priority: string; sla: string }): void };
    };
    component.filtersForm.setValue({ savedViewId: '', savedViewName: '', view: 'All', search: '', status: 'Resolved', category: '', assigned: 'Mine', priority: 'Urgent', sla: 'Overdue' });

    const form = (fixture.nativeElement as HTMLElement).querySelector('form');
    form?.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Closed case');
    expect(supportService.listQueue).toHaveBeenCalledWith(jasmine.objectContaining({
      view: 'All',
      status: 'Resolved',
      assigned: 'Mine',
      priority: 'Urgent',
      sla: 'Overdue'
    }));
  });

  it('claims support tickets from the queue', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const button = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .find(item => item.textContent?.includes('Claim'));
    button?.dispatchEvent(new Event('click'));
    await fixture.whenStable();

    expect(supportService.claimTicket).toHaveBeenCalledWith('ticket-id');
  });

  it('saves support queue views and exports CSV', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      filtersForm: { patchValue(value: { savedViewName: string; category: string }): void };
    };
    component.filtersForm.patchValue({ savedViewName: 'Urgent order tickets', category: 'OrderIssue' });

    const buttons = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'));
    buttons.find(button => button.textContent?.includes('Save view'))?.dispatchEvent(new Event('click'));
    await fixture.whenStable();
    buttons.find(button => button.textContent?.includes('Export CSV'))?.dispatchEvent(new Event('click'));
    await fixture.whenStable();

    expect(supportService.createSavedView).toHaveBeenCalledWith(jasmine.objectContaining({
      queue: 'Support',
      name: 'Urgent order tickets',
      filters: jasmine.objectContaining({ view: 'NeedsAttention', category: 'OrderIssue' })
    }));
    expect(supportService.exportQueue).toHaveBeenCalled();
  });

  it('loads and exports support quality reporting', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('SLA outcome reporting');
    expect(compiled.textContent).toContain('First response');
    expect(supportService.getQualityReport).toHaveBeenCalled();

    const buttons = Array.from(compiled.querySelectorAll('button'));
    buttons.find(button => button.textContent?.includes('Export quality CSV'))?.dispatchEvent(new Event('click'));
    await fixture.whenStable();

    expect(supportService.exportQualityReport).toHaveBeenCalled();
  });
});

export function createAdminSupportTicket(
  overrides: Partial<AdminSupportTicketResponse> = {}
): AdminSupportTicketResponse {
  return {
    supportTicketId: 'ticket-id',
    createdByUserId: 'buyer-user-id',
    createdByRole: 'Buyer',
    buyerId: 'buyer-id',
    sellerId: null,
    category: 'OrderIssue',
    status: 'Open',
    priority: 'Normal',
    subject: 'Damaged order',
    description: 'The order arrived damaged.',
    linkedOrderId: 'order-id',
    linkedProductId: null,
    linkedSellerId: null,
    linkedPaymentId: null,
    assignedSupportUserId: null,
    escalationReason: null,
    escalatedAtUtc: null,
    escalatedByUserId: null,
    openedAtUtc: '2026-05-19T10:00:00Z',
    resolvedAtUtc: null,
    closedAtUtc: null,
    customerContext: null,
    messages: [{
      supportMessageId: 'message-id',
      senderUserId: 'buyer-user-id',
      senderRole: 'Buyer',
      message: 'The order arrived damaged.',
      isInternal: false,
      createdAtUtc: '2026-05-19T10:00:00Z'
    }],
    ...overrides
  };
}

function createQueue(items: AdminSupportQueueItemResponse[]): AdminSupportQueueResponse {
  return {
    items,
    totalCount: items.length,
    page: 1,
    pageSize: 25,
    statusCounts: [{ key: 'Open', count: items.length }],
    priorityCounts: [{ key: 'Normal', count: items.length }],
    slaCounts: [{ key: 'OnTrack', count: items.length }]
  };
}

function createQueueItem(overrides: Partial<AdminSupportQueueItemResponse> = {}): AdminSupportQueueItemResponse {
  const ticket = createAdminSupportTicket(overrides as Partial<AdminSupportTicketResponse>);
  return {
    ...ticket,
    assignedSupportDisplayName: null,
    updatedAtUtc: '2026-05-19T10:00:00Z',
    latestInternalNote: null,
    messageCount: ticket.messages.length,
    ageHours: 1,
    slaStatus: 'OnTrack',
    slaDueAtUtc: '2026-05-20T10:00:00Z',
    ...overrides
  };
}

function createSummary(): AdminSupportSummaryResponse {
  return {
    generatedAtUtc: '2026-05-19T11:00:00Z',
    openTicketCount: 1,
    escalatedTicketCount: 0,
    overdueTicketCount: 0,
    myOpenTicketCount: 0,
    unassignedOpenTicketCount: 1,
    resolvedTodayCount: 0,
    resolvedLast7DaysCount: 0,
    averageFirstResponseHours: null,
    averageResolutionHours: null,
    statusCounts: [{ key: 'Open', count: 1 }],
    priorityCounts: [{ key: 'Normal', count: 1 }],
    slaCounts: [{ key: 'OnTrack', count: 1 }],
    assigneeCounts: [{ assignedSupportUserId: 'Unassigned', assignedSupportDisplayName: null, count: 1 }]
  };
}

function createSavedView(overrides: Partial<{ viewId: string; queue: string; name: string; isDefault: boolean }> = {}) {
  return {
    viewId: overrides.viewId ?? 'view-id',
    queue: overrides.queue ?? 'Support',
    name: overrides.name ?? 'Urgent support',
    isDefault: overrides.isDefault ?? false,
    filters: {
      view: 'NeedsAttention',
      status: null,
      category: 'OrderIssue',
      search: null,
      sellerId: null,
      assigned: 'Mine',
      priority: 'Urgent',
      hasNotes: null,
      sla: 'Overdue',
      sort: 'PriorityDesc',
      pageSize: 25
    },
    createdAtUtc: '2026-05-19T10:00:00Z',
    updatedAtUtc: '2026-05-19T10:00:00Z'
  };
}

function createQualityReport() {
  return {
    generatedAtUtc: '2026-05-19T11:00:00Z',
    fromUtc: '2026-04-19T11:00:00Z',
    toUtc: '2026-05-19T11:00:00Z',
    bucket: 'Day',
    summary: {
      createdCount: 1,
      resolvedCount: 1,
      closedCount: 0,
      escalatedCount: 0,
      currentlyOpenCount: 0,
      currentlyOverdueCount: 0,
      averageFirstResponseHours: 1,
      averageResolutionHours: 4,
      firstResponseTargetMetCount: 1,
      firstResponseTargetMissedCount: 0,
      resolutionTargetMetCount: 1,
      resolutionTargetMissedCount: 0
    },
    trend: [{
      bucketStartUtc: '2026-05-19T00:00:00Z',
      bucketEndUtc: '2026-05-20T00:00:00Z',
      createdCount: 1,
      resolvedCount: 1,
      escalatedCount: 0,
      averageFirstResponseHours: 1,
      averageResolutionHours: 4
    }],
    categoryBreakdown: [{
      key: 'OrderIssue',
      createdCount: 1,
      resolvedCount: 1,
      escalatedCount: 0,
      firstResponseTargetMissedCount: 0,
      resolutionTargetMissedCount: 0,
      averageFirstResponseHours: 1,
      averageResolutionHours: 4
    }],
    priorityBreakdown: [],
    requesterRoleBreakdown: [],
    slaBreakdown: [],
    assigneeBreakdown: [{
      assignedSupportUserId: 'support-user-id',
      assignedSupportDisplayName: 'Support Agent',
      createdCount: 1,
      resolvedCount: 1,
      escalatedCount: 0,
      firstResponseTargetMissedCount: 0,
      resolutionTargetMissedCount: 0,
      averageFirstResponseHours: 1,
      averageResolutionHours: 4
    }]
  };
}
