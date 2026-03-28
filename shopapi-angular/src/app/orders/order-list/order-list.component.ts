// src/app/orders/order-list/order-list.component.ts
import { Component, inject, OnInit } from '@angular/core';
import { CommonModule }              from '@angular/common';
import { RouterLink, Router }        from '@angular/router';
import { OrderService }              from '../../services/order.service';

@Component({
  selector:   'app-order-list',
  standalone: true,
  imports:    [CommonModule, RouterLink],
    templateUrl: './order-list.component.html',
  styleUrl: './order-list.component.css'
})
export class OrderListComponent implements OnInit {

  private orderService = inject(OrderService);
  private router       = inject(Router);

  orders        : any[] = [];
  isLoading     = true;
  successMessage = '';
  cancellingId?: number;

  ngOnInit(): void {
    // Check if coming from checkout with success
    const nav   = this.router.getCurrentNavigation();
    const state = nav?.extras?.state as any;
    if (state?.successOrderId) {
      this.successMessage =
        `Order #${state.successOrderId} placed successfully! 🎉`;
    }

    this.loadOrders();
  }

  loadOrders(): void {
    this.isLoading = true;
    this.orderService.getMyOrders().subscribe({
      next: (res) => {
        this.orders    = res.orders || res;
        this.isLoading = false;
      },
      error: () => { this.isLoading = false; }
    });
  }

  cancelOrder(id: number): void {
    if (!confirm('Cancel this order?')) return;
    this.cancellingId = id;

    this.orderService.cancelOrder(id).subscribe({
      next: () => {
        this.cancellingId = undefined;
        this.loadOrders(); // reload list
      },
      error: (err) => {
        this.cancellingId = undefined;
        alert(err.error?.error || 'Failed to cancel order.');
      }
    });
  }

  getStatusClass(status: string): string {
    const classes: { [key: string]: string } = {
      'Pending':   'bg-warning text-dark',
      'Confirmed': 'bg-info text-dark',
      'Shipped':   'bg-primary',
      'Delivered': 'bg-success',
      'Cancelled': 'bg-danger'
    };
    return classes[status] || 'bg-secondary';
  }
}