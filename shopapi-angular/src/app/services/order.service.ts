// src/app/services/order.service.ts
// ─────────────────────────────────────────────────────
// ORDER SERVICE = all order-related API calls
// ─────────────────────────────────────────────────────
import { Injectable, inject } from '@angular/core';
import { HttpClient }         from '@angular/common/http';
import { Observable }         from 'rxjs';
import { CreateOrderDto,
         Order }              from '../models/auth.models';

@Injectable({
  providedIn: 'root'
})
export class OrderService {

  private http = inject(HttpClient);
  private readonly API = 'https://localhost:7245/api';

  // ── Place new order ──
  placeOrder(dto: CreateOrderDto): Observable<any> {
    return this.http.post(`${this.API}/order`, dto);
  }

  // ── Get customer's own orders ──
  getMyOrders(): Observable<any> {
    return this.http.get(`${this.API}/order/my-orders`);
  }

  // ── Get single order ──
  getOrderById(id: number): Observable<Order> {
    return this.http.get<Order>(
      `${this.API}/order/my-orders/${id}`
    );
  }

  // ── Cancel order ──
  cancelOrder(id: number): Observable<any> {
    return this.http.post(
      `${this.API}/order/my-orders/${id}/cancel`, {}
    );
  }

  // ── Update order status (Seller/Admin) ──
  updateOrderStatus(id: number, status: number): Observable<any> {
    return this.http.put(
      `${this.API}/order/${id}/status`,
      { status }
    );
  }
}