// src/app/app.component.ts
// ─────────────────────────────────────────────────────
// APP COMPONENT = Root component of the entire app
//
// CONCEPT: standalone: true
//   Component manages its own imports
//   No need to declare in any NgModule
//
// CONCEPT: RouterOutlet
//   Placeholder where routed components appear
//   When user goes to /login → LoginComponent appears here
//
// CONCEPT: AsyncPipe (| async)
//   Subscribes to Observable automatically in template
//   Unsubscribes automatically when component is destroyed
//   Prevents memory leaks!
// ─────────────────────────────────────────────────────
import { Component, inject, OnInit } from '@angular/core';
import { RouterOutlet, RouterLink,
         RouterLinkActive }          from '@angular/router';
import { AsyncPipe, CommonModule }   from '@angular/common';
import { AuthService }               from './services/auth.service';

@Component({
  selector: 'app-root', // used in index.html as <app-root>
  standalone: true,     // Angular 19 = standalone by default
  imports: [
    RouterOutlet, // <router-outlet> in template
    RouterLink, // [routerLink] directive
    RouterLinkActive, // | async pipe
    CommonModule
],
   templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {

  // inject() = Angular 19 way to use services
  authService = inject(AuthService);

  // Store current user info
  currentUser: any = null;

  ngOnInit(): void {
    // Load user info when app starts
    this.currentUser = this.authService.getCurrentUser();

    // Subscribe to login state changes
    // When user logs in/out → update currentUser
    this.authService.isLoggedIn$.subscribe(() => {
      this.currentUser = this.authService.getCurrentUser();
    });
  }

  // Get user-friendly role label for navbar badge
  getRoleLabel(): string {
    if (this.authService.isAdmin())    return 'Admin';
    if (this.authService.isSeller())   return 'Seller';
    if (this.authService.isCustomer()) return 'Customer';
    return '';
  }

  // Logout button handler
  logout(): void {
    this.authService.logout();
    this.currentUser = null;
  }
}