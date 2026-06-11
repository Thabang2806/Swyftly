import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { getApiErrorMessage } from '../auth/api-error';
import { SellerReturnRequestResult } from '../seller/seller-return.models';
import { SellerReturnService } from '../seller/seller-return.service';
import { SellerWorkspaceNavComponent } from '../seller/seller-workspace-nav.component';
import { EmptyStateComponent } from '../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header.component';
import { StatusBadgeComponent, StatusBadgeTone } from '../shared/ui/status-badge.component';
import { UiAlertComponent } from '../shared/ui/ui-alert.component';

@Component({
  selector: 'app-seller-returns-page',
  imports: [
    DatePipe,
    EmptyStateComponent,
    PageHeaderComponent,
    RouterLink,
    SellerWorkspaceNavComponent,
    StatusBadgeComponent,
    UiAlertComponent
  ],
  template: `
    <section class="page seller-ops-page">
      <app-seller-workspace-nav />

      <app-page-header
        eyebrow="Seller operations"
        heading="Returns"
        description="Review return requests and respond from the seller side."
      />

      @if (isLoading()) {
        <div class="route-card">Loading returns...</div>
      } @else {
        @if (errorMessage()) {
          <app-ui-alert tone="error">{{ errorMessage() }}</app-ui-alert>
        }

        @if (returns().length === 0 && !errorMessage()) {
          <app-empty-state
            eyebrow="Returns"
            heading="No return requests"
            message="Return requests linked to your orders will appear here."
          />
        } @else {
          <div class="admin-table seller-ops-table" role="table" aria-label="Seller returns">
            <div class="admin-table-row heading seller-ops-table-row" role="row">
              <span role="columnheader">Return</span>
              <span role="columnheader">Order</span>
              <span role="columnheader">Items</span>
              <span role="columnheader">Status</span>
              <span role="columnheader">Action</span>
            </div>

            @for (returnRequest of returns(); track returnRequest.returnRequestId) {
              <div class="admin-table-row seller-ops-table-row" role="row">
                <span role="cell">
                  <strong>{{ returnRequest.reason }}</strong>
                  <small>{{ returnRequest.requestedAtUtc | date:'medium' }}</small>
                </span>
                <span role="cell">
                  <strong>{{ returnRequest.orderId }}</strong>
                  @if (returnRequest.details) {
                    <small>{{ returnRequest.details }}</small>
                  }
                </span>
                <span role="cell">
                  <strong>{{ itemCount(returnRequest) }} item{{ itemCount(returnRequest) === 1 ? '' : 's' }}</strong>
                  <small>{{ returnRequest.items.length }} line{{ returnRequest.items.length === 1 ? '' : 's' }}</small>
                </span>
                <span role="cell">
                  <app-status-badge [label]="returnRequest.status" [tone]="statusTone(returnRequest.status)" />
                  @if (returnRequest.sellerRespondedAtUtc) {
                    <small>Responded {{ returnRequest.sellerRespondedAtUtc | date:'mediumDate' }}</small>
                  }
                </span>
                <span role="cell">
                  <a data-ui-button="secondary" [routerLink]="['/returns', returnRequest.returnRequestId]">Open</a>
                </span>
              </div>
            }
          </div>
        }
      }
    </section>
  `
})
export class SellerReturnsPageComponent implements OnInit {
  private readonly returnService = inject(SellerReturnService);

  protected readonly returns = signal<SellerReturnRequestResult[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    await this.loadReturns();
  }

  protected itemCount(returnRequest: SellerReturnRequestResult): number {
    return returnRequest.items.reduce((total, item) => total + item.quantity, 0);
  }

  protected statusTone(status: string): StatusBadgeTone {
    if (['Requested', 'AwaitingSellerResponse', 'ReturnInTransit', 'ReturnedToSeller', 'RefundPending'].includes(status)) {
      return 'warning';
    }

    if (['Approved', 'Refunded', 'Closed'].includes(status)) {
      return 'success';
    }

    if (['Rejected', 'Disputed'].includes(status)) {
      return 'danger';
    }

    return 'neutral';
  }

  private async loadReturns(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      this.returns.set(await this.returnService.listReturns());
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }
}
