import { SellerPolicyResponse } from '../shared/seller-policy.models';

export interface ProductSearchResponse {
  items: ProductSearchItemResponse[];
  page: number;
  pageSize: number;
  totalCount: number;
  sort: string;
}

export interface ProductSearchItemResponse {
  productId: string;
  sellerId: string;
  sellerStoreName: string | null;
  sellerStoreSlug: string | null;
  categoryId: string | null;
  categoryPath: string | null;
  brandId: string | null;
  title: string | null;
  slug: string | null;
  shortDescription: string | null;
  merchandisingLabel?: string | null;
  primaryImageUrl: string | null;
  primaryImageAltText: string | null;
  priceMin: number;
  compareAtPriceMin: number | null;
  inStock: boolean;
  tags: string[];
  publishedAtUtc: string | null;
}

export interface ProductSearchRequest {
  query?: string | null;
  categoryId?: string | null;
  categorySlug?: string | null;
  sellerId?: string | null;
  minPrice?: number | null;
  maxPrice?: number | null;
  size?: string | null;
  colour?: string | null;
  brandId?: string | null;
  material?: string | null;
  inStock?: boolean | null;
  sort?: string | null;
  page?: number | null;
  pageSize?: number | null;
}

export interface PublicProductDetailResponse {
  product: ProductSearchItemResponse;
  fullDescription: string | null;
  seoTitle?: string | null;
  seoDescription?: string | null;
  careInstructions?: string | null;
  productDisclaimer?: string | null;
  attributes: Record<string, string>;
  images: PublicProductImageResponse[];
  variants: PublicProductVariantResponse[];
  sellerPolicy: SellerPolicyResponse;
}

export interface PublicProductImageResponse {
  imageId: string;
  url: string;
  altText: string | null;
  isPrimary: boolean;
}

export interface PublicProductVariantResponse {
  variantId: string;
  size: string;
  colour: string;
  price: number;
  compareAtPrice: number | null;
  inStock: boolean;
}

export interface PublicCategoryResponse {
  categoryId: string;
  parentCategoryId: string | null;
  name: string;
  slug: string;
  displayOrder: number;
}

export interface PublicSellerStorefrontResponse {
  sellerId: string;
  storeName: string;
  slug: string;
  description: string | null;
  logoUrl: string | null;
  bannerUrl: string | null;
  products: ProductSearchItemResponse[];
  sellerPolicy: SellerPolicyResponse;
}
