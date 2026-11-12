// Triggered by EgoGuard. Close every tab in the user's Spaces; Ego opens its default page next time.
import { readFile } from 'node:fs/promises';

const requestFile = new URL('./guard-request.json', import.meta.url);
let request;
async function stillClosed() {
  try {
    const current = JSON.parse(await readFile(requestFile, 'utf8'));
    request ??= current;
    return current.id === request.id && Date.now() < current.expires;
  } catch {
    return false;
  }
}

let closed = 0;
for (const user of (await listTaskSpaces()).filter(space => space.ownership === 'user')) {
  if (!(await stillClosed())) break;
  let task;
  let complete = false;
  try {
    task = await claimTaskSpace(user.id);
    const tabs = await task.tabs();
    if (!(await stillClosed())) break;
    const last = tabs.pop();
    // Close all but one tab at once; this does not take over pages, so the page budget does not apply.
    const results = await Promise.allSettled(tabs.map(tab => task.cdp('Target.closeTarget', { targetId: tab.targetId })));
    closed += results.filter(result => result.status === 'fulfilled').length;
    if (last && (await stillClosed())) {
      // Close the last tab through the SDK so the Space can be ended cleanly.
      const page = last.label ? task.page(last.label) : await task.adopt(last.page);
      await page.close();
      closed++;
      complete = true;
    }
  } finally {
    const remaining = (await listTaskSpaces()).find(space => space.id === user.id);
    // keep: [] ends the emptied Space; after a cancellation, hand back what is left.
    if (task && remaining?.ownership === 'agent') await task.finish({ keep: complete ? [] : 'all' });
  }
}
console.log('EGO_RESET_RESULT ' + JSON.stringify({ closed, cancelled: !(await stillClosed()) }));
