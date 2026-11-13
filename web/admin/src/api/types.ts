/**
 * Contratos JSON del backend (camelCase), copiados de los records de
 * AuthEndpoints, CatalogContracts y CustomerContracts. Las claves de
 * `errors` en un 400 llegan en PascalCase (`Code`, `Phone`, `File`).
 * Cuando la API pueda emitir OpenAPI sin una base real, este archivo
 * se sustituye por la salida de openapi-typescript.
 */

export type Membership = {
  tenantId: number
  tenantCode: string
  tenantName: string
  role: string
}

export type AuthResponse = {
  accessToken: string
  expiresIn: number
  refreshToken: string | null
  tenantId: number | null
  memberships: Membership[]
  requiresPasswordChange: boolean
}

export type Me = {
  userId: number
  login: string
  displayName: string
  requiresPasswordChange: boolean
  tenantId: number | null
  role: string | null
  permissions: string[]
  memberships: Membership[]
}

export type Paged<T> = {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export type Category = {
  id: number
  name: string
  isActive: boolean
  imageKey: string | null
  imageUrl: string | null
  version: number
  updatedAtUtc: string | null
}

export type Product = {
  id: number
  code: string
  name: string
  categoryId: number | null
  categoryName: string | null
  unitPrice: number
  packPrice: number
  unitsPerPack: number
  sellsByUnit: boolean
  isActive: boolean
  imageKey: string | null
  imageUrl: string | null
  version: number
  updatedAtUtc: string | null
}

export type Promotion = {
  id: number
  title: string
  description: string
  code: string
  productId: number | null
  productName: string | null
  unitPrice: number
  packPrice: number
  sellsByUnit: boolean
  priority: number
  isActive: boolean
  imageKey: string | null
  imageUrl: string | null
  createdAtUtc: string
  version: number
  updatedAtUtc: string | null
}

export type Establishment = {
  id: number
  name: string
  address: string | null
  isActive: boolean
  version: number
  updatedAtUtc: string | null
}

export type Customer = {
  id: number
  name: string
  identification: string | null
  phone: string
  address: string | null
  zone: string | null
  establishmentId: number | null
  establishmentName: string | null
  registeredAtUtc: string | null
  isActive: boolean
  version: number
  updatedAtUtc: string | null
}

export type CustomerSave = {
  customer: Customer
  samePhoneCustomerIds: number[]
}

export type PhoneCheck = {
  isValid: boolean
  phone: string | null
  samePhoneCustomerIds: number[]
}

export type OrderLine = {
  id: number
  productId: number | null
  name: string
  kind: string
  quantity: number
  unitPrice: number
  subtotal: number
}

export type Order = {
  id: number
  number: string
  status: string
  statusLabel: string
  createdAt: string
  customerId: number
  customerName: string | null
  phone: string | null
  address?: string | null
  note?: string | null
  total: number
  lines?: OrderLine[]
}

export const pageSize = 20

export const limits = {
  code: 50,
  categoryName: 100,
  productName: 200,
  promotionTitle: 150,
  promotionDescription: 1000,
  name: 150,
  identification: 30,
  address: 255,
  zone: 60,
  maxPrice: 9_999_999_999.99,
  maxUnitsPerPack: 10_000,
} as const
