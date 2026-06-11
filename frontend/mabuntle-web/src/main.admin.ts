import { bootstrapApplication } from '@angular/platform-browser';
import { AdminAppComponent } from './app/admin-app.component';
import { createAppConfig } from './app/app-config.factory';
import { adminRoutes } from './app/admin.routes';

bootstrapApplication(AdminAppComponent, createAppConfig(adminRoutes, 'admin'))
  .catch((err) => console.error(err));
