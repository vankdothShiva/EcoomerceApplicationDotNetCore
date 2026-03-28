// src/app/orders/checkout/checkout.component.ts
import { Component, inject, OnInit } from '@angular/core';
import { CommonModule }              from '@angular/common';
import { Router, RouterLink }        from '@angular/router';
import { FormsModule }               from '@angular/forms';
import { OrderService }              from '../../services/order.service';

@Component({
  selector:   'app-checkout',
  standalone: true,
  imports:    [CommonModule, FormsModule, RouterLink],
   templateUrl: './checkout.component.html',
  styleUrl: './checkout.component.css'
})
export class CheckoutComponent implements OnInit {

  private orderService = inject(OrderService);
  private router       = inject(Router);

  cart:         any[] = [];
  shipAddress   = '';
  isLoading     = false;
  errorMessage  = '';

  ngOnInit(): void {
    // Load cart from localStorage
    const saved = localStorage.getItem('shopapi_cart');
    this.cart   = saved ? JSON.parse(saved) : [];
  }

  getTotal(): number {
    return this.cart.reduce(
      (sum, item) => sum + (item.product.price * item.quantity),
      0
    );
  }

  decreaseQty(item: any): void {
    if (item.quantity > 1) {
      item.quantity--;
    } else {
      this.removeItem(item);
    }
  }

  removeItem(item: any): void {
    this.cart = this.cart.filter(
      i => i.product.id !== item.product.id
    );
    localStorage.setItem(
      'shopapi_cart', JSON.stringify(this.cart)
    );
  }

  placeOrder(): void {
    if (!this.shipAddress.trim()) {
      this.errorMessage = 'Please enter a shipping address.';
      return;
    }

    this.isLoading    = true;
    this.errorMessage = '';

    const dto = {
      shipAddress: this.shipAddress,
      items: this.cart.map(item => ({
        productId: item.product.id,
        quantity:  item.quantity
      }))
    };

    this.orderService.placeOrder(dto).subscribe({
      next: (res) => {
        this.isLoading = false;
        // Clear cart after successful order
        localStorage.removeItem('shopapi_cart');
        // Navigate to orders page
        this.router.navigate(['/orders'], {
          state: { successOrderId: res.orderId }
        });
      },
      error: (err) => {
        this.isLoading    = false;
        this.errorMessage =
          err.error?.error || 'Failed to place order.';
      }
    });
  }
}