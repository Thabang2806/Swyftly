import { bootstrapApplication } from '@angular/platform-browser';
import { createAppConfig } from './app/app-config.factory';
import { SellerAppComponent } from './app/seller-app.component';
import { sellerRoutes } from './app/seller.routes';

bootstrapApplication(SellerAppComponent, createAppConfig(sellerRoutes, 'seller'))
  .catch((err) => console.error(err));
