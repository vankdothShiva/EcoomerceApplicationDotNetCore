// src/app/profile/profile.component.ts
import { Component, inject, OnInit } from '@angular/core';
import { CommonModule }              from '@angular/common';
import { RouterLink }                from '@angular/router';
import {
  ReactiveFormsModule,
  FormGroup,
  FormControl,
  Validators
}                                    from '@angular/forms';
import { AuthService }               from '../services/auth.service';
import { UserProfile }               from '../models/auth.models';

@Component({
  selector:   'app-profile',
  standalone: true,
  imports:    [CommonModule, ReactiveFormsModule, /*PENDING*/],
templateUrl: './profile.component.html',
  styleUrl: './profile.component.css'
})
export class ProfileComponent implements OnInit {

  private authService = inject(AuthService);

  profile?: UserProfile;
  isLoading         = true;
  isSaving          = false;
  isChangingPassword= false;
  updateSuccess     = false;
  updateError       = '';
  passwordSuccess   = false;
  passwordError     = '';

  profileForm = new FormGroup({
    firstName: new FormControl(''),
    lastName:  new FormControl(''),
    phone:     new FormControl(''),
    city:      new FormControl(''),
    address:   new FormControl('')
  });

  passwordForm = new FormGroup({
    currentPassword: new FormControl('', Validators.required),
    newPassword:     new FormControl('', [
      Validators.required,
      Validators.minLength(8)
    ]),
    confirmPassword: new FormControl('', Validators.required)
  });

  ngOnInit(): void {
    this.authService.getProfile().subscribe({
      next: (profile) => {
        this.profile   = profile;
        this.isLoading = false;
        // Fill form with current values
        this.profileForm.patchValue({
          firstName: profile.firstName,
          lastName:  profile.lastName,
          phone:     profile.phoneNumber || '',
          city:      profile.city        || '',
          address:   profile.address     || ''
        });
      },
      error: () => { this.isLoading = false; }
    });
  }

  getInitials(): string {
    if (!this.profile) return '?';
    return `${this.profile.firstName[0]}${this.profile.lastName[0]}`
      .toUpperCase();
  }

  updateProfile(): void {
    this.isSaving     = true;
    this.updateSuccess= false;
    this.updateError  = '';

    this.authService.updateProfile(
      this.profileForm.value
    ).subscribe({
      next: () => {
        this.isSaving      = false;
        this.updateSuccess = true;
        setTimeout(() => this.updateSuccess = false, 3000);
      },
      error: (err) => {
        this.isSaving    = false;
        this.updateError = err.error?.error || 'Update failed.';
      }
    });
  }

  changePassword(): void {
    const f = this.passwordForm.value;
    if (f.newPassword !== f.confirmPassword) {
      this.passwordError = 'Passwords do not match.';
      return;
    }

    this.isChangingPassword = true;
    this.passwordError      = '';
    this.passwordSuccess    = false;

    this.authService.changePassword({
      currentPassword: f.currentPassword!,
      newPassword:     f.newPassword!,
      confirmPassword: f.confirmPassword!
    }).subscribe({
      next: () => {
        this.isChangingPassword = false;
        this.passwordSuccess    = true;
        this.passwordForm.reset();
      },
      error: (err) => {
        this.isChangingPassword = false;
        this.passwordError =
          err.error?.error || 'Failed to change password.';
      }
    });
  }
}