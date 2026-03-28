// src/app/services/product.service.ts
// ─────────────────────────────────────────────────────
// PRODUCT SERVICE = all product-related API calls
//
// CONCEPT: HttpParams
//   Used to build query strings dynamically
//   ?search=phone&category=Electronics&page=1
//
// CONCEPT: Observable<T>
//   T = the type of data the Observable emits
//   Observable<ProductListResponse> means it will
//   eventually give us a ProductListResponse object
// ─────────────────────────────────────────────────────
import { Injectable, inject }  from '@angular/core';
import { HttpClient,
         HttpParams }          from '@angular/common/http';
import { Observable }          from 'rxjs';
import {
  Product,
  ProductListResponse,
  CreateProductDto
}                              from '../models/auth.models';

@Injectable({
  providedIn: 'root'
})
export class ProductService {

  private http = inject(HttpClient);
  private readonly API = 'https://localhost:7245/api';

  // ── Get all products with filters ──
  getProducts(filters: {
    search?:   string;
    category?: string;
    minPrice?: number;
    maxPrice?: number;
    page?:     number;
    pageSize?: number;
  } = {}): Observable<ProductListResponse> {

    // Build query params dynamically
    let params = new HttpParams();

    if (filters.search)
      params = params.set('search',   filters.search);
    if (filters.category)
      params = params.set('category', filters.category);
    if (filters.minPrice)
      params = params.set('minPrice', filters.minPrice);
    if (filters.maxPrice)
      params = params.set('maxPrice', filters.maxPrice);

    params = params.set('page',
      filters.page?.toString()     || '1');
    params = params.set('pageSize',
      filters.pageSize?.toString() || '10');

    return this.http.get<ProductListResponse>(
      `${this.API}/product`, { params }
    );
  }

  // ── Get single product ──
  getProduct(id: number): Observable<Product> {
    return this.http.get<Product>(
      `${this.API}/product/${id}`
    );
  }

  // ── Get all categories ──
  getCategories(): Observable<{ categories: string[] }> {
    return this.http.get<{ categories: string[] }>(
      `${this.API}/product/categories`
    );
  }

  // ── Get seller's own products ──
  getMyProducts(): Observable<any> {
    return this.http.get(`${this.API}/product/my-products`);
  }

  // ── Create product (Seller) ──
  createProduct(dto: CreateProductDto): Observable<any> {
    return this.http.post(`${this.API}/product`, dto);
  }

  // ── Update product (Seller) ──
  updateProduct(id: number, dto: any): Observable<any> {
    return this.http.put(`${this.API}/product/${id}`, dto);
  }

  // ── Delete product (Admin) ──
  deleteProduct(id: number): Observable<any> {
    return this.http.delete(
      `${this.API}/admin/products/${id}`
    );
  }
}