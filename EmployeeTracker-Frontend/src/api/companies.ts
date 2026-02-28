import { apiDelete, apiGet, apiPost, apiPut } from "./client";
import { Device } from "./devices";

export interface Company {
  id: string;
  name: string;
  isActive: boolean;
  deviceCount: number;
  enrollmentKey: string | null;
}

export interface CreateCompanyResponse {
  id: string;
  name: string;
  isActive: boolean;
  enrollmentKey: string;
}

export async function getCompanies(): Promise<Company[]> {
  return apiGet<Company[]>("/companies");
}

export async function getCompanyDevices(companyId: string): Promise<Device[]> {
  return apiGet<Device[]>(`/companies/${encodeURIComponent(companyId)}/devices`);
}

export async function createCompany(
  name: string,
  enrollmentKey: string,
  adminEmail: string,
  adminPassword: string,
  isActive: boolean = true
): Promise<CreateCompanyResponse> {
  return apiPost<CreateCompanyResponse>("/companies", {
    name,
    enrollmentKey,
    adminEmail,
    adminPassword,
    isActive,
  });
}

export interface UpdateCompanyResponse {
  id: string;
  name: string;
  isActive: boolean;
  deviceCount: number;
  enrollmentKey: string | null;
}

export async function updateCompany(
  companyId: string,
  name: string,
  enrollmentKey: string,
  isActive: boolean
): Promise<UpdateCompanyResponse> {
  return apiPut<UpdateCompanyResponse>(`/companies/${encodeURIComponent(companyId)}`, {
    name,
    enrollmentKey,
    isActive,
  });
}

export async function deleteCompany(companyId: string): Promise<{ deleted: boolean }> {
  return apiDelete<{ deleted: boolean }>(`/companies/${encodeURIComponent(companyId)}`);
}
