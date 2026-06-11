import { createAppConfig } from './app-config.factory';
import { clientRoutes } from './client.routes';

export const appConfig = createAppConfig(clientRoutes, 'client');
