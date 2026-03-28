// src/app/services/admin.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient }         from '@angular/common/http';
import { Observable }         from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class AdminService {

  private http = inject(HttpClient);
  private readonly API = 'https://localhost:7245/api/admin';

  // ── Dashboard stats ──
  getDashboard(): Observable<any> {
    return this.http.get(`${this.API}/dashboard`);
  }

  // ── Get all users ──
  getUsers(page = 1, pageSize = 10): Observable<any> {
    return this.http.get(
      `${this.API}/users?page=${page}&pageSize=${pageSize}`
    );
  }

  // ── Get single user ──
  getUser(id: string): Observable<any> {
    return this.http.get(`${this.API}/users/${id}`);
  }

  // ── Assign role ──
  assignRole(userId: string, role: string): Observable<any> {
    return this.http.post(
      `${this.API}/users/${userId}/assign-role`,
      JSON.stringify(role),
      { headers: { 'Content-Type': 'application/json' } }
    );
  }

  // ── Toggle user active/inactive ──
  toggleUserActive(userId: string): Observable<any> {
    return this.http.put(
      `${this.API}/users/${userId}/toggle-active`, {}
    );
  }

  // ── Get all orders ──
  getAllOrders(status = '', page = 1): Observable<any> {
    return this.http.get(
      `${this.API}/orders?status=${status}&page=${page}`
    );
  }
}