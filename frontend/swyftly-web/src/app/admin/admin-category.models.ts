export interface AdminCategoryResponse {
  categoryId: string;
  parentCategoryId: string | null;
  name: string;
  slug: string;
  displayOrder: number;
  isActive: boolean;
  productCount: number;
  childCount: number;
  attributes: AdminCategoryAttributeResponse[];
}

export interface AdminCategoryAttributeResponse {
  attributeId: string;
  name: string;
  key: string;
  dataType: string;
  isRequired: boolean;
  allowedValues: string[];
  displayOrder: number;
  isActive: boolean;
}

export interface UpsertAdminCategoryRequest {
  parentCategoryId: string | null;
  name: string;
  slug: string;
  displayOrder: number;
}

export interface UpsertAdminCategoryAttributeRequest {
  name: string;
  key: string;
  dataType: string;
  isRequired: boolean;
  allowedValues: string[];
  displayOrder: number;
}
