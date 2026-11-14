import {describe, expect, it} from 'vitest';
import {isApiError} from '@/lib/api';
import {readTimelineEvent, readPatientDocument, readTimelinePage, readDocumentPage} from './patient-readers';
const event = {id:'e1', eventType:'note', occurredAtUtc:'2026-11-14T10:00:00Z', visitId:null, visit:null, note:{doctorName:'Doctor',noteContent:'Test note'}, labResult:null, document:null};
const doc = {id:'d1',fileName:'scan.pdf', type:'Pdf',uploadDateUtc:'2026-11-14T10:00:00Z', description:null,uploadedByDoctorName:null,location:'C:/SECRET'};
const page = (items: unknown[]) => ({items,page:1,pageSize:10,totalCount:items.length,totalPages:items.length ? 1 : 0,hasPreviousPage:false,hasNextPage:false});
describe('timeline and documents API boundaries', () => {
 it('accepts an event of exactly one type', () => expect(readTimelineEvent(event).note?.noteContent).toBe('Test note'));
 it('rejects mismatched and missing event payloads', () => { for(const bad of [{...event,eventType:'document'}, {...event,note:null}]) {try {readTimelineEvent(bad); throw new Error('should have failed');} catch(e){expect(isApiError(e) && e.kind==='invalid-response').toBe(true);}} });
 it('does not pass through document storage location', () => expect(readPatientDocument(doc)).not.toHaveProperty('location'));
 it('accepts metadata-only documents without an uploader', () => expect(readPatientDocument(doc).uploadedByDoctorName).toBeNull());
 it('rejects malformed documents', () => expect(() => readPatientDocument({...doc,fileName:null})).toThrow());
 it('reads paged timeline and document results', () => {expect(readTimelinePage(page([event])).items).toHaveLength(1);expect(readDocumentPage(page([doc])).items).toHaveLength(1);});
});
