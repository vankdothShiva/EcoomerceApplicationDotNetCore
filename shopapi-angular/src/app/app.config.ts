// src/app/app.config.ts
// ─────────────────────────────────────────────────────
// APP CONFIG = root configuration for Angular 19
//
// CONCEPT: ApplicationConfig
//   Replaces the old AppModule
//   Provides all app-wide services and features
//
// CONCEPT: provideHttpClient()
//   Sets up Angular's HTTP system
//   withInterceptors() adds our auth interceptor
//
// CONCEPT: provideRouter()
//   Sets up Angular's routing system
//   withComponentInputBinding() allows route params
//   as component inputs (Angular 16+ feature)
// ─────────────────────────────────────────────────────
import { ApplicationConfig,
         provideZoneChangeDetection } from '@angular/core';
import { provideRouter,
         withComponentInputBinding } from '@angular/router';
import { provideHttpClient,
         withInterceptors }          from '@angular/common/http';
import { routes }                    from './app.routes';
import { authInterceptor }           from './interceptors/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [

    // ── Zone change detection (performance optimization) ──
    provideZoneChangeDetection({ eventCoalescing: true }),

    // ── Setup routing with all our routes ──
    provideRouter(
      routes,
      withComponentInputBinding()
      // Allows @Input() to receive route params directly
    ),

    // ── Setup HTTP client with our auth interceptor ──
    provideHttpClient(
      withInterceptors([authInterceptor])
      // Every HTTP request goes through authInterceptor
      // which adds Authorization: Bearer token header
    ),
  ]
};