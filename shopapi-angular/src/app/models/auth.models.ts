// src/app/models/auth.models.ts
// ─────────────────────────────────────────────────────
// MODELS = TypeScript interfaces that match our API DTOs
// Think of them as "shapes" of data we send/receive
// ─────────────────────────────────────────────────────

// ── What we send to API when registering ──
export interface RegisterDto {
  firstName: string;
  lastName:  string;
  email:     string;
  password:  string;
  address?:  string;   // ? means optional
  city?:     string;
  role:      string;   // "Customer" or "Seller"
}

// ── What we send when logging in ──
export interface LoginDto {
  email:    string;
  password: string;
}

// ── What API sends back after login ──
export interface AuthResponse {
  accessToken:  string;   // JWT token for API calls
  refreshToken: string;   // used to get new access token
  email:        string;
  fullName:     string;
  roles:        string[]; // ["Admin"] or ["Customer"] etc
  expiresAt:    string;
  requiresMfa:  boolean;  // true = needs OTP verification
  userId?:      string;   // only set when requiresMfa=true
}

// ── What we send for MFA OTP verification ──
export interface VerifyOtpDto {
  userId: string;
  otp:    string; // 6-digit code
}

// ── What we send when changing password ──
export interface ChangePasswordDto {
  currentPassword: string;
  newPassword:     string;
  confirmPassword: string;
}

// ── What we send for password reset ──
export interface ResetPasswordDto {
  userId:          string;
  token:           string;
  newPassword:     string;
  confirmPassword: string;
}

// ── Shape of user profile from API ──
export interface UserProfile {
  id:                        string;
  firstName:                 string;
  lastName:                  string;
  email:                     string;
  phoneNumber?:              string;
  address?:                  string;
  city?:                     string;
  createdAt:                 string;
  twoFactorEnabled:          boolean;
  authenticatorSetupComplete: boolean;
  roles:                     string[];
}

// ── Shape of a product from API ──
export interface Product {
  id:          number;
  name:        string;
  description: string;
  price:       number;
  stock:       number;
  category:    string;
  isActive:    boolean;
  createdAt:   string;
  sellerName:  string;
  sellerId:    string;
}

// ── What API returns for product list ──
export interface ProductListResponse {
  totalCount: number;
  page:       number;
  pageSize:   number;
  totalPages: number;
  products:   Product[];
}

// ── What we send when creating a product ──
export interface CreateProductDto {
  name:        string;
  description: string;
  price:       number;
  stock:       number;
  category:    string;
}

// ── Shape of an order ──
export interface Order {
  id:          number;
  customerId:  string;
  customerName:string;
  status:      string;
  totalAmount: number;
  shipAddress: string;
  createdAt:   string;
  items:       OrderItem[];
}

// ── Single item inside an order ──
export interface OrderItem {
  productId:   number;
  productName: string;
  quantity:    number;
  unitPrice:   number;
  subTotal:    number;
}

// ── What we send when placing an order ──
export interface CreateOrderDto {
  shipAddress?: string;
  items: {
    productId: number;
    quantity:  number;
  }[];
}

// ── Admin dashboard stats ──
export interface DashboardStats {
  stats: {
    totalUsers:    number;
    totalProducts: number;
    totalOrders:   number;
    totalRevenue:  number;
  };
  recentOrders: any[];
}