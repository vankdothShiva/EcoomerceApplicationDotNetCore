// src/app/services/auth.service.ts
// ─────────────────────────────────────────────────────
// AUTH SERVICE = handles all authentication API calls
//
// CONCEPT: HttpClient
//   Angular service for making HTTP requests
//   Returns Observables (like Promises but more powerful)
//
// CONCEPT: Observable + tap()
//   Observable = stream of data over time
//   tap() = do something with data WITHOUT changing it
//   pipe() = chain operators together
//
// CONCEPT: BehaviorSubject
//   Special Observable that:
//   1. Always has a current value
//   2. Emits to new subscribers immediately
//   Used to track login state across components
// ─────────────────────────────────────────────────────
import { Injectable, inject }            from '@angular/core';
import { HttpClient }                    from '@angular/common/http';
import { Router }                        from '@angular/router';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { TokenService }                  from './token.service';
import {
  RegisterDto, LoginDto, AuthResponse,
  VerifyOtpDto, ChangePasswordDto,
  ResetPasswordDto, UserProfile
} from '../models/auth.models';

@Injectable({
  providedIn: 'root'
})
export class AuthService {

  // ── inject() is Angular 19 way to inject services ──
  // Alternative to constructor injection
  private http         = inject(HttpClient);
  private router       = inject(Router);
  private tokenService = inject(TokenService);

  // Base URL of your ASP.NET Core backend
  private readonly API = 'https://localhost:7245/api';

  // ── BehaviorSubject to track login state ──
  // true = logged in, false = logged out
  // initialized with current login state from localStorage
  private loggedIn$ = new BehaviorSubject<boolean>(
    this.tokenService.isLoggedIn()
  );

  // ── Expose as read-only Observable ──
  // Components subscribe to this to react to login/logout
  get isLoggedIn$(): Observable<boolean> {
    return this.loggedIn$.asObservable();
  }

  // ── REGISTER: create new account ──
  // Returns Observable<any> because response is just a message
  register(dto: RegisterDto): Observable<any> {
    return this.http.post(`${this.API}/auth/register`, dto);
  }

  // ── CONFIRM EMAIL: called from email link ──
  confirmEmail(userId: string, token: string): Observable<any> {
    return this.http.get(
      `${this.API}/auth/confirm-email`,
      { params: { userId, token } }
      // params = query string: ?userId=...&token=...
    );
  }

  // ── LOGIN: authenticate user ──
  login(dto: LoginDto): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(
      `${this.API}/auth/login`, dto
    ).pipe(
      // tap = side effect: runs after response received
      // but does NOT modify the response
      tap(response => {
        if (!response.requiresMfa) {
          // No MFA required → save tokens immediately
          this.saveSession(response);
        }
        // If MFA required → don't save yet!
        // User must verify OTP first
        // MFA component handles saving after verification
      })
    );
  }

  // ── VERIFY EMAIL OTP: step 2 when MFA enabled ──
  verifyEmailOtp(dto: VerifyOtpDto): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(
      `${this.API}/mfa/email-otp/verify`, dto
    ).pipe(
      tap(response => this.saveSession(response))
      // Save session after successful OTP verification
    );
  }

  // ── VERIFY AUTHENTICATOR: step 2 with Google Auth app ──
  verifyAuthenticator(dto: VerifyOtpDto): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(
      `${this.API}/mfa/authenticator/verify`, dto
    ).pipe(
      tap(response => this.saveSession(response))
    );
  }

  // ── REFRESH TOKEN: get new access token ──
  // Called automatically by interceptor when token expires
  refreshToken(): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(
      `${this.API}/auth/refresh-token`, {
        accessToken:  this.tokenService.getAccessToken(),
        refreshToken: this.tokenService.getRefreshToken()
      }
    ).pipe(
      tap(response => this.saveSession(response))
    );
  }

  // ── FORGOT PASSWORD: send reset email ──
  forgotPassword(email: string): Observable<any> {
    return this.http.post(
      `${this.API}/auth/forgot-password`,
      { email }
    );
  }

  // ── RESET PASSWORD: set new password using token ──
  resetPassword(dto: ResetPasswordDto): Observable<any> {
    return this.http.post(
      `${this.API}/auth/reset-password`, dto
    );
  }

  // ── GET PROFILE: fetch current user details ──
  getProfile(): Observable<UserProfile> {
    return this.http.get<UserProfile>(`${this.API}/profile`);
    // Interceptor automatically adds Authorization header
  }

  // ── UPDATE PROFILE ──
  updateProfile(dto: any): Observable<any> {
    return this.http.put(`${this.API}/profile`, dto);
  }

  // ── CHANGE PASSWORD ──
  changePassword(dto: ChangePasswordDto): Observable<any> {
    return this.http.post(
      `${this.API}/profile/change-password`, dto
    );
  }

  // ── LOGOUT: clear tokens and redirect ──
  logout(): void {
    const refreshToken = this.tokenService.getRefreshToken();

    // Tell server to revoke the refresh token
    if (refreshToken) {
      this.http.post(
        `${this.API}/auth/logout`,
        JSON.stringify(refreshToken),
        { headers: { 'Content-Type': 'application/json' } }
      ).subscribe({
        error: () => {} // ignore errors on logout
      });
    }

    // Clear all stored tokens
    this.tokenService.clearAll();

    // Update login state → all subscribers notified
    this.loggedIn$.next(false);

    // Redirect to login page
    this.router.navigate(['/auth/login']);
  }

  // ── Helper: save session data after login ──
  private saveSession(response: AuthResponse): void {
    // Save JWT tokens to localStorage
    this.tokenService.saveTokens(
      response.accessToken,
      response.refreshToken
    );

    // Save user info
    this.tokenService.saveUser({
      email:    response.email,
      fullName: response.fullName,
      roles:    response.roles
    });

    // Notify all subscribers that user is logged in
    this.loggedIn$.next(true);
  }

  // ── Convenience methods for components ──
  isLoggedIn():          boolean  { return this.tokenService.isLoggedIn(); }
  getCurrentUser():      any      { return this.tokenService.getUser(); }
  getRoles():            string[] { return this.tokenService.getRoles(); }
  hasRole(role: string): boolean  { return this.tokenService.hasRole(role); }
  isAdmin():             boolean  { return this.hasRole('Admin'); }
  isSeller():            boolean  { return this.hasRole('Seller'); }
  isCustomer():          boolean  { return this.hasRole('Customer'); }
}