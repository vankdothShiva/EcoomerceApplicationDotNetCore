



// src/app/products/product-list/product-list.component.ts
// ─────────────────────────────────────────────────────
// PRODUCT LIST = main shopping page
//
// CONCEPT: debounceTime
//   Waits 400ms after user stops typing before searching
//   Prevents API call on every keystroke
//
// CONCEPT: Subject
//   Like BehaviorSubject but no initial value
//   We use it to trigger search when user types
// ─────────────────────────────────────────────────────
import { Component, inject,
         OnInit, OnDestroy }   from '@angular/core';
import { CommonModule }        from '@angular/common';
import { RouterLink }          from '@angular/router';
import { FormsModule }         from '@angular/forms';
import { Subject, Subscription } from 'rxjs';
import { debounceTime,
         distinctUntilChanged } from 'rxjs/operators';
import { ProductService }      from '../../services/product.service';
import { OrderService }        from '../../services/order.service';
import { AuthService }         from '../../services/auth.service';
import { Product }             from '../../models/auth.models';

@Component({
  selector:   'app-product-list',
  standalone: true,
  imports:    [CommonModule, RouterLink, FormsModule],
  templateUrl: './product-list.component.html',
  styleUrl: './product-list.component.css'

})
export class ProductListComponent implements OnInit, OnDestroy {

  productService = inject(ProductService);
  authService    = inject(AuthService);
  orderService   = inject(OrderService);

  // ── State ──
  products:         Product[] = [];
  categories:       string[]  = [];
  isLoading         = true;
  totalCount        = 0;
  totalPages        = 1;
  currentPage       = 1;
  cartMessage       = '';

  // ── Filter values ──
  searchText        = '';
  selectedCategory  = '';
  minPrice?: number;
  maxPrice?: number;

  // ── Cart (stored in localStorage) ──
  cart: { product: Product; quantity: number }[] = [];

  // ── Search debounce ──
  // Subject to detect search text changes
  private searchSubject = new Subject<string>();
  private searchSub!:   Subscription;

  ngOnInit(): void {
    this.loadCategories();
    this.loadProducts();
    this.loadCart();

    // Set up debounced search
    // Wait 400ms after user stops typing
    this.searchSub = this.searchSubject.pipe(
      debounceTime(400),        // wait 400ms
      distinctUntilChanged()    // only if value changed
    ).subscribe(() => {
      this.currentPage = 1;    // reset to page 1
      this.loadProducts();
    });
  }

  ngOnDestroy(): void {
    // Clean up subscription to prevent memory leaks
    this.searchSub?.unsubscribe();
  }

  // ── Load categories for filter dropdown ──
  loadCategories(): void {
    this.productService.getCategories().subscribe({
      next: (res) => this.categories = res.categories,
      error: ()  => {}
    });
  }

  // ── Load products from API ──
  loadProducts(): void {
    this.isLoading = true;

    this.productService.getProducts({
      search:   this.searchText   || undefined,
      category: this.selectedCategory || undefined,
      minPrice: this.minPrice,
      maxPrice: this.maxPrice,
      page:     this.currentPage,
      pageSize: 12
    }).subscribe({
      next: (res) => {
        this.products   = res.products;
        this.totalCount = res.totalCount;
        this.totalPages = res.totalPages;
        this.isLoading  = false;
      },
      error: () => {
        this.isLoading = false;
      }
    });
  }

  // ── Trigger search via Subject ──
  onSearchChange(value: string): void {
    this.searchSubject.next(value);
  }

  // ── Apply filters ──
  applyFilters(): void {
    this.currentPage = 1;
    this.loadProducts();
  }

  // ── Pagination ──
  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages) return;
    this.currentPage = page;
    this.loadProducts();
    // Scroll to top
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  getPageNumbers(): number[] {
    // Show max 5 page numbers
    const pages: number[] = [];
    const start = Math.max(1, this.currentPage - 2);
    const end   = Math.min(this.totalPages, start + 4);
    for (let i = start; i <= end; i++) pages.push(i);
    return pages;
  }

  // ── Add to cart ──
  addToCart(product: Product): void {
    const existing = this.cart.find(
      item => item.product.id === product.id
    );

    if (existing) {
      existing.quantity++;
    } else {
      this.cart.push({ product, quantity: 1 });
    }

    // Save cart to localStorage
    localStorage.setItem('shopapi_cart', JSON.stringify(this.cart));

    // Show notification
    this.cartMessage = `${product.name} added to cart!`;
    setTimeout(() => this.cartMessage = '', 3000);
  }

  // ── Load cart from localStorage ──
  loadCart(): void {
    const saved = localStorage.getItem('shopapi_cart');
    this.cart = saved ? JSON.parse(saved) : [];
  }

  // ── Get emoji for product category ──
  getCategoryEmoji(category: string): string {
    const emojis: { [key: string]: string } = {
      'Electronics': '📱',
      'Clothing':    '👕',
      'Food':        '🍕',
      'Books':       '📚',
      'Sports':      '⚽',
      'Home':        '🏠',
      'Beauty':      '💄',
      'Toys':        '🧸',
    };
    return emojis[category] || '📦';
  }
}