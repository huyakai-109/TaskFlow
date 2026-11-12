/**
 * Shared by every page-level keyboard shortcut (`C` new task, `J`/`K`
 * next/previous task, `⌘↵` primary action) so none of them fires while the
 * user is typing into a field. Extracted here at its third inline copy
 * (`BoardPage`, `TaskDetailPage`, now `Topbar`) — coding-standards.md "an
 * abstraction at the second real use, not the first".
 */
export function isTypingTarget(target: EventTarget | null): boolean {
  return target instanceof HTMLElement && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable)
}
