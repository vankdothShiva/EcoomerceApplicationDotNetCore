




// src/app/auth/register/register.component.ts
// ─────────────────────────────────────────────────────
// REGISTER COMPONENT = new user registration
//
// CONCEPT: Custom Validator
//   We create our own validation function
//   to check if passwords match
//
// CONCEPT: FormGroup validator vs FormControl validator
//   FormControl validator = validates single field
//   FormGroup validator   = validates relationship
//                           between multiple fields
// ─────────────────────────────────────────────────────
import { Component, inject }    from '@angular/core';
import { CommonModule }         from '@angular/common';
import { RouterLink, Router }   from '@angular/router';
import {
  ReactiveFormsModule,
  FormGroup,
  FormControl,
  Validators,
  AbstractControl,
  ValidationErrors
}                               from '@angular/forms';
import { AuthService }          from '../../services/auth.service';
import { RegisterDto }          from '../../models/auth.models';

// ── Custom Validator: check passwords match ──
// This is a FormGroup-level validator
// It receives the entire form group and checks fields
function passwordMatchValidator(
  control: AbstractControl
): ValidationErrors | null {
  const password        = control.get('password');
  const confirmPassword = control.get('confirmPassword');

  // If passwords don't match → return error object
  if (password?.value !== confirmPassword?.value) {
    // Set error on confirmPassword control
    confirmPassword?.setErrors({ passwordMismatch: true });
    return { passwordMismatch: true };
  }

  // Clear the error if they match
  confirmPassword?.setErrors(null);
  return null; // null = no error
}

@Component({
  selector:   'app-register',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink
  ],
  templateUrl: './register.component.html',
  styleUrl: './register.component.css'
})
export class RegisterComponent {

  private authService = inject(AuthService);
  private router      = inject(Router);

  isLoading      = false;
  showPassword   = false;
  errorMessage   = '';
  successMessage = '';

  // ── Register Form with custom validator ──
  registerForm = new FormGroup({
    firstName: new FormControl('', [
      Validators.required,
      Validators.minLength(2)
    ]),
    lastName: new FormControl('', [
      Validators.required,
      Validators.minLength(2)
    ]),
    email: new FormControl('', [
      Validators.required,
      Validators.email
    ]),
    password: new FormControl('', [
      Validators.required,
      Validators.minLength(8),
      // Pattern: must have uppercase, digit, special char
      Validators.pattern(
        /^(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$/
      )
    ]),
    confirmPassword: new FormControl('', [
      Validators.required
    ]),
    role: new FormControl('Customer'),
    city: new FormControl('')
  },
  // FormGroup-level validator (checks password match)
  { validators: passwordMatchValidator }
  );

  get f() { return this.registerForm.controls; }

  isFieldInvalid(field: string): boolean {
    const control = this.registerForm.get(field);
    return !!(control?.invalid &&
             (control.dirty || control.touched));
  }

  // ── Set role when card is clicked ──
  setRole(role: string): void {
    this.registerForm.patchValue({ role });
  }

  // ── Password Strength Calculator ──
  getPasswordStrength(): number {
    const password = this.f['password'].value || '';
    let strength   = 0;

    if (password.length >= 8)           strength += 25;
    if (/[A-Z]/.test(password))         strength += 25;
    if (/[0-9]/.test(password))         strength += 25;
    if (/[@$!%*?&]/.test(password))     strength += 25;

    return strength;
  }

  getPasswordStrengthClass(): string {
    const strength = this.getPasswordStrength();
    if (strength <= 25) return 'bg-danger';
    if (strength <= 50) return 'bg-warning';
    if (strength <= 75) return 'bg-info';
    return 'bg-success';
  }

  getPasswordStrengthLabel(): string {
    const strength = this.getPasswordStrength();
    if (strength <= 25) return 'Weak';
    if (strength <= 50) return 'Fair';
    if (strength <= 75) return 'Good';
    return 'Strong ✅';
  }

  // ── Handle registration form submit ──
  onSubmit(): void {
    this.registerForm.markAllAsTouched();
    if (this.registerForm.invalid) return;

    this.isLoading    = true;
    this.errorMessage = '';

    const dto: RegisterDto = {
      firstName: this.f['firstName'].value!,
      lastName:  this.f['lastName'].value!,
      email:     this.f['email'].value!,
      password:  this.f['password'].value!,
      city:      this.f['city'].value || undefined,
      role:      this.f['role'].value!
    };

    this.authService.register(dto).subscribe({
      next: () => {
        this.isLoading    = false;
        this.successMessage =
          `We sent a confirmation email to ${dto.email}. ` +
          `Please click the link in the email to activate ` +
          `your account before logging in.`;
      },
      error: (err) => {
        this.isLoading = false;
        // Handle array of errors from API
        if (err.error?.errors) {
          this.errorMessage = Object.values(err.error.errors)
            .flat()
            .join(' ');
        } else {
          this.errorMessage =
            err.error?.error ||
            err.error?.message ||
            'Registration failed. Please try again.';
        }
      }
    });
  }
}