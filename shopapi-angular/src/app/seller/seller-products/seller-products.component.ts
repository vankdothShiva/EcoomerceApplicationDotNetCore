// src/app/seller/seller-products/seller-products.component.ts
import { Component, inject, OnInit } from '@angular/core';
import { CommonModule }              from '@angular/common';
import {
  ReactiveFormsModule,
  FormGroup,
  FormControl,
  Validators
}                                    from '@angular/forms';
import { ProductService }            from '../../services/product.service';

@Component({
  selector:   'app-seller-products',
  standalone: true,
  imports:    [CommonModule, ReactiveFormsModule],
  templateUrl: './seller-products.component.html',
  styleUrl: './seller-products.component.css'
})
export class SellerProductsComponent implements OnInit {

  private productService = inject(ProductService);

  products:    any[] = [];
  isLoading    = true;
  showForm     = false;
  isAdding     = false;
  formSuccess  = false;
  formError    = '';

  productForm = new FormGroup({
    name:        new FormControl('', Validators.required),
    description: new FormControl('', Validators.required),
    price:       new FormControl('', [
      Validators.required,
      Validators.min(0.01)
    ]),
    stock:       new FormControl('', [
      Validators.required,
      Validators.min(0)
    ]),
    category:    new FormControl('', Validators.required)
  });

  ngOnInit(): void {
    this.loadMyProducts();
  }

  loadMyProducts(): void {
    this.isLoading = true;
    this.productService.getMyProducts().subscribe({
      next: (res) => {
        this.products  = res.products || res;
        this.isLoading = false;
      },
      error: () => { this.isLoading = false; }
    });
  }

  addProduct(): void {
    if (this.productForm.invalid) return;

    this.isAdding   = true;
    this.formError  = '';
    this.formSuccess= false;

    const dto = {
      name:        this.productForm.value.name!,
      description: this.productForm.value.description!,
      price:       Number(this.productForm.value.price),
      stock:       Number(this.productForm.value.stock),
      category:    this.productForm.value.category!
    };

    this.productService.createProduct(dto).subscribe({
      next: () => {
        this.isAdding    = false;
        this.formSuccess = true;
        this.productForm.reset();
        this.loadMyProducts(); // refresh list
        setTimeout(() => {
          this.showForm    = false;
          this.formSuccess = false;
        }, 2000);
      },
      error: (err) => {
        this.isAdding  = false;
        this.formError =
          err.error?.error || 'Failed to add product.';
      }
    });
  }
}