
// src/app/auth/mfa/mfa.component.ts
// ─────────────────────────────────────────────────────
// MFA COMPONENT = handles 2-step verification
//
// CONCEPT: Router Navigation State
//   When navigating: router.navigate(['/mfa'], { state: {...} })
//   Receive it:      router.getCurrentNavigation()?.extras.state
//   Used to pass data between pages without URL params
// ─────────────────────────────────────────────────────
import { Component, inject, OnInit } from '@angular/core';
import { CommonModule }              from '@angular/common';
import { Router, RouterLink }        from '@angular/router';
import {
  ReactiveFormsModule,
  FormGroup,
  FormControl,
  Validators
}                                    from '@angular/forms';
import { AuthService }               from '../../services/auth.service';

@Component({
  selector:   'app-mfa',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './mfa.component.html',
  styleUrl: './mfa.component.css'
})
export class MfaComponent implements OnInit {

  private authService = inject(AuthService);
  private router      = inject(Router);

  isLoading      = false;
  errorMessage   = '';
  activeTab      = 'email'; // 'email' or 'authenticator'
  userId         = '';
  email          = '';
  resendCooldown = 0; // countdown timer for resend button

  otpForm = new FormGroup({
    otp: new FormControl('', [
      Validators.required,
      Validators.minLength(6),
      Validators.maxLength(6),
      Validators.pattern(/^\d{6}$/) // must be 6 digits
    ])
  });

  ngOnInit(): void {
    // Get data passed from login page via router state
    const navigation = this.router.getCurrentNavigation();
    const state = navigation?.extras?.state as any;

    if (state) {
      this.userId  = state.userId  || '';
      this.email   = state.email   || '';
      this.activeTab = state.mfaType || 'email';
    }

    // If no userId → user came directly, redirect to login
    if (!this.userId) {
      this.router.navigate(['/auth/login']);
    }
  }

  get f() { return this.otpForm.controls; }

  isFieldInvalid(field: string): boolean {
    const control = this.otpForm.get(field);
    return !!(control?.invalid &&
             (control.dirty || control.touched));
  }

  onSubmit(): void {
    this.otpForm.markAllAsTouched();
    if (this.otpForm.invalid) return;

    this.isLoading    = true;
    this.errorMessage = '';

    const dto = {
      userId: this.userId,
      otp:    this.f['otp'].value!
    };

    // Choose which verification method based on active tab
    const verify$ = this.activeTab === 'email'
      ? this.authService.verifyEmailOtp(dto)
      : this.authService.verifyAuthenticator(dto);

    verify$.subscribe({
      next: (response) => {
        this.isLoading = false;
        // Redirect based on role
        if (response.roles.includes('Admin')) {
          this.router.navigate(['/admin/dashboard']);
        } else if (response.roles.includes('Seller')) {
          this.router.navigate(['/seller/products']);
        } else {
          this.router.navigate(['/products']);
        }
      },
      error: (err) => {
        this.isLoading = false;
        this.errorMessage =
          err.error?.error ||
          'Invalid code. Please try again.';
        this.otpForm.reset(); // clear input for retry
      }
    });
  }

  // ── Resend OTP with cooldown timer ──
  resendOtp(): void {
    // TODO: call resend API
    // Start 60-second cooldown to prevent spam
    this.resendCooldown = 60;
    const timer = setInterval(() => {
      this.resendCooldown--;
      if (this.resendCooldown <= 0) {
        clearInterval(timer);
      }
    }, 1000);
  }
}