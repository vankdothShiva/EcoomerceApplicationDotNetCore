



// src/app/auth/confirm-email/confirm-email.component.ts
// Called when user clicks the link in confirmation email
import { Component, inject, OnInit } from '@angular/core';
import { CommonModule }              from '@angular/common';
import { Router, ActivatedRoute,
         RouterLink }                from '@angular/router';
import { AuthService }               from '../../services/auth.service';

@Component({
  selector:   'app-confirm-email',
  standalone: true,
  imports:    [CommonModule, RouterLink],
  templateUrl: './confirm-email.component.html',
  styleUrl: './confirm-email.component.css'
})
export class ConfirmEmailComponent implements OnInit {

  private authService = inject(AuthService);
  private route       = inject(ActivatedRoute);

  isLoading    = true;
  isSuccess    = false;
  errorMessage = '';

  ngOnInit(): void {
    // Read userId and token from URL query params
    // URL: /auth/confirm-email?userId=...&token=...
    this.route.queryParams.subscribe(params => {
      const userId = params['userId'];
      const token  = params['token'];

      if (!userId || !token) {
        this.isLoading    = false;
        this.isSuccess    = false;
        this.errorMessage = 'Invalid confirmation link.';
        return;
      }

      // Call API to confirm email
      this.authService.confirmEmail(userId, token).subscribe({
        next: () => {
          this.isLoading = false;
          this.isSuccess = true;
        },
        error: (err) => {
          this.isLoading    = false;
          this.isSuccess    = false;
          this.errorMessage =
            err.error?.error ||
            'Link may be expired. Please register again.';
        }
      });
    });
  }
}