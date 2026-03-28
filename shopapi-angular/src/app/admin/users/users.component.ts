// src/app/admin/users/users.component.ts
import { Component, inject, OnInit } from '@angular/core';
import { CommonModule }              from '@angular/common';
import { FormsModule }               from '@angular/forms';
import { AdminService }              from '../../services/admin.service';

@Component({
  selector:   'app-users',
  standalone: true,
  imports:    [CommonModule, FormsModule],
   templateUrl: './users.component.html',
  styleUrl: './users.component.css'
})
export class UsersComponent implements OnInit {

  private adminService = inject(AdminService);

  users:       any[] = [];
  isLoading    = true;
  currentPage  = 1;
  totalCount   = 0;
  hasMore      = false;

  ngOnInit(): void {
    this.loadPage(1);
  }

  loadPage(page: number): void {
    this.isLoading   = true;
    this.currentPage = page;

    this.adminService.getUsers(page).subscribe({
      next: (res) => {
        this.users      = res.users;
        this.totalCount = res.totalCount;
        this.hasMore    = page < res.totalPages;
        this.isLoading  = false;
      },
      error: () => { this.isLoading = false; }
    });
  }

  assignRole(userId: string, event: Event): void {
    const role = (event.target as HTMLSelectElement).value;
    if (!role) return;

    this.adminService.assignRole(userId, role).subscribe({
      next: () => this.loadPage(this.currentPage),
      error: (err) => alert(err.error?.error || 'Failed.')
    });
  }

  toggleActive(user: any): void {
    this.adminService.toggleUserActive(user.id).subscribe({
      next: () => {
        user.isActive = !user.isActive;
      },
      error: (err) => alert(err.error?.error || 'Failed.')
    });
  }
}