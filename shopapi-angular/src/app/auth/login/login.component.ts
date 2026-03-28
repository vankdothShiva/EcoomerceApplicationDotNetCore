  // src/app/auth/login/login.component.ts
// ─────────────────────────────────────────────────────
// LOGIN COMPONENT = handles user login
//
// CONCEPT: ReactiveFormsModule
//   FormGroup  = container for all form controls
//   FormControl= individual input field
//   Validators = built-in validation rules
//   formGroup  = links FormGroup to <form> in HTML
//   formControlName = links FormControl to <input>
//
// CONCEPT: Component Lifecycle
//   ngOnInit() = runs once when component loads
//   Used to initialize forms, fetch data etc.
//
// CONCEPT: (click) / (submit)
//   Event binding = calls method when event happens
// ─────────────────────────────────────────────────────
import { Component, inject, OnInit } from '@angular/core';
import { CommonModule }              from '@angular/common';
import { RouterLink, Router,
         ActivatedRoute }            from '@angular/router';
import {
  ReactiveFormsModule,
  FormGroup,
  FormControl,
  Validators
}                                    from '@angular/forms';
import { AuthService }               from '../../services/auth.service';
import { LoginDto }                  from '../../models/auth.models';

@Component({
  selector:    'app-login',
  standalone:  true, // Angular 19 standalone
  imports: [
    CommonModule,        // *ngIf, *ngFor
    ReactiveFormsModule, // formGroup, formControlName
    RouterLink,          // [routerLink]
  ],
   templateUrl: './login.component.html',
  styleUrl: './login.component.css'
  
})
export class LoginComponent implements OnInit {

  // ── Inject services using Angular 19 inject() ──
  private authService = inject(AuthService);
  private router      = inject(Router);
  private route       = inject(ActivatedRoute);

  // ── State variables ──
  isLoading      = false;  // shows spinner on button
  showPassword   = false;  // toggles password visibility
  errorMessage   = '';     // shows error alert
  successMessage = '';     // shows success alert

  // ── Build the Login Form ──
  // FormGroup = container with all controls
  loginForm = new FormGroup({

    // FormControl(defaultValue, [validators])
    email: new FormControl('', [
      Validators.required,       // cannot be empty
      Validators.email           // must be valid email format
    ]),

    password: new FormControl('', [
      Validators.required        // cannot be empty
    ])
  });

  ngOnInit(): void {
    // Check if redirected from registration
    // route.queryParams reads ?registered=true from URL
    this.route.queryParams.subscribe(params => {
      if (params['registered']) {
        this.successMessage =
          '✅ Registration successful! Please check your ' +
          'email to confirm your account before logging in.';
      }
      if (params['confirmed']) {
        this.successMessage =
          '✅ Email confirmed! You can now login.';
      }
    });

    // If already logged in → redirect to products
    if (this.authService.isLoggedIn()) {
      this.router.navigate(['/products']);
    }
  }

  // ── Shortcut to access form controls in template ──
  // f['email'] instead of loginForm.controls['email']
  get f() {
    return this.loginForm.controls;
  }

  // ── Check if a field is invalid AND was touched ──
  // We only show errors after user has interacted
  isFieldInvalid(field: string): boolean {
    const control = this.loginForm.get(field);
    return !!(control?.invalid &&
             (control.dirty || control.touched));
    // dirty   = user typed something
    // touched = user clicked and left the field
  }

  // ── Handle form submission ──
  onSubmit(): void {
    // Mark all fields as touched to show validation errors
    this.loginForm.markAllAsTouched();

    // Stop if form is invalid
    if (this.loginForm.invalid) return;

    this.isLoading    = true;
    this.errorMessage = '';

    // Build the DTO from form values
    const dto: LoginDto = {
      email:    this.f['email'].value!,
      password: this.f['password'].value!
    };

    // Call API
    this.authService.login(dto).subscribe({

      next: (response) => {
        this.isLoading = false;

        if (response.requiresMfa) {
          // MFA required → go to MFA page
          // Pass userId via navigation state
          this.router.navigate(['/auth/mfa'], {
            state: {
              userId:   response.userId,
              email:    dto.email,
              mfaType:  'email' // or 'authenticator'
            }
          });
        } else {
          // No MFA → redirect based on role
          this.redirectAfterLogin(response.roles);
        }
      },

      error: (err) => {
        this.isLoading = false;
        // Show error from API or generic message
        this.errorMessage =
          err.error?.error ||
          err.error?.message ||
          'Login failed. Please try again.';
      }
    });
  }

  // ── Redirect user to correct page based on role ──
  private redirectAfterLogin(roles: string[]): void {
    if (roles.includes('Admin')) {
      this.router.navigate(['/admin/dashboard']);
    } else if (roles.includes('Seller')) {
      this.router.navigate(['/seller/products']);
    } else {
      this.router.navigate(['/products']);
    }
  }

  // ── Fill test account credentials quickly ──
  fillTestAccount(type: 'admin' | 'seller' | 'customer'): void {
    const accounts = {
      admin:    { email: 'admin@shopapi.com',    password: 'Admin@123456'    },
      seller:   { email: 'seller@shopapi.com',   password: 'Seller@123456'   },
      customer: { email: 'customer@shopapi.com', password: 'Customer@123456' }
    };
    const account = accounts[type];
    this.loginForm.patchValue(account);
    // patchValue = fill form fields with values
  }
}