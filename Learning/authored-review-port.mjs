// Authored content must describe its recorded review, rather than inherit the
// original upstream template's fixed AI-review claim. Keep that source pinned.
export function authoredLessonReviewHTML(lesson, esc) {
  const value = field => typeof field === 'string' ? field.trim() : '';
  const review = lesson.review ?? {};
  const parts = [
    value(review.authorship) || value(lesson.authorship),
    value(review.status) && 'Review status: ' + value(review.status),
    value(review.date) && 'Review date: ' + value(review.date),
    value(review.reviewer) && 'Reviewer: ' + value(review.reviewer)
  ].filter(Boolean);
  return parts.length ? `<p class="small">${parts.map(esc).join(' — ')}</p>` : '';
}

export function authoredReviewPort(source) {
  source = source.replace(/\r\n?/g, '\n');
  const once = (before, after) => {
    if (source.split(before).length !== 2) throw Error('Review the authored tutor review-label port: upstream lesson-guide.mjs has changed.');
    source = source.replace(before, after);
  };
  once('<p class="small">Original StatsDirect teaching material. AI-assisted editorial review ${esc(lesson.review?.date)}.</p>', '');
  once('  return html;', '  html+=authoredLessonReviewHTML(lesson,esc);\n  return html;');
  return "import {authoredLessonReviewHTML} from '../../authored-review-port.mjs';\n" + source;
}
