import {apiRequest, createInvalidResponseError} from '@/lib/api';

export type Testimonial = {
  id: string;
  quoteAr: string;
  quoteEn: string;
  nameAr: string;
  nameEn: string;
  roleAr: string;
  roleEn: string;
};

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

export async function getTestimonials(locale: 'ar' | 'en'): Promise<Testimonial[]> {
  const data = await apiRequest<unknown>('/api/testimonials');
  if (!Array.isArray(data)) {
    throw createInvalidResponseError(200);
  }

  return data.map((item) => {
    if (!isRecord(item)) {
      throw createInvalidResponseError(200);
    }
    const fields = locale === 'ar'
      ? ['id', 'quoteAr', 'nameAr', 'roleAr']
      : ['id', 'quoteEn', 'nameEn', 'roleEn'];
    if (fields.some((field) => typeof item[field] !== 'string')) {
      throw createInvalidResponseError(200);
    }
    return item as unknown as Testimonial;
  });
}
