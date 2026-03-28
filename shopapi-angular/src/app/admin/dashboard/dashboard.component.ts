// src/app/admin/dashboard/dashboard.component.ts
import { Component, inject, OnInit } from '@angular/core';
import { CommonModule }              from '@angular/common';
import { RouterLink }                from '@angular/router';
import { AdminService }              from '../../services/admin.service';

@Component({
  selector:   'app-dashboard',
  standalone: true,
  imports:    [CommonModule, RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent implements OnInit {

  private adminService = inject(AdminService);

  stats:        any  = null;
  recentOrders: any[] = [];
  isLoading     = true;

  ngOnInit(): void {
    this.adminService.getDashboard().subscribe({
      next: (res) => {
        this.stats        = res.stats;
        this.recentOrders = res.recentOrders;
        this.isLoading    = false;
      },
      error: () => { this.isLoading = false; }
    });
  }

  getStatusClass(status: string): string {
    const map: { [k: string]: string } = {
      'Pending':   'bg-warning text-dark',
      'Confirmed': 'bg-info text-dark',
      'Shipped':   'bg-primary',
      'Delivered': 'bg-success',
      'Cancelled': 'bg-danger'
    };
    return map[status] || 'bg-secondary';
  }
}