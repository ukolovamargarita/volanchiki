import type { ReactNode } from "react";

/** Лёгкий рендер: абзацы по \\n\\n / \\n и **жирный**. Без HTML от модели. */
export function RichText({ text, className }: { text: string; className?: string }) {
  const paragraphs = splitParagraphs(text);
  if (!paragraphs.length) return null;

  return (
    <div className={"rich" + (className ? " " + className : "")}>
      {paragraphs.map((p, i) => (
        <p key={i}>{renderInline(p)}</p>
      ))}
    </div>
  );
}

export function RichInline({ text }: { text: string }) {
  return <>{renderInline(text)}</>;
}

function splitParagraphs(raw: string): string[] {
  const normalized = raw.replace(/\r\n/g, "\n").trim();
  if (!normalized) return [];

  // сначала двойные переносы, иначе одиночные (модель часто пишет \n)
  const byBlank = normalized.split(/\n{2,}/).map((s) => s.trim()).filter(Boolean);
  if (byBlank.length > 1) return byBlank;

  const byLine = normalized.split(/\n/).map((s) => s.trim()).filter(Boolean);
  return byLine.length > 1 ? byLine : [normalized];
}

function renderInline(s: string): ReactNode[] {
  const parts: ReactNode[] = [];
  const re = /\*\*(.+?)\*\*/g;
  let last = 0;
  let m: RegExpExecArray | null;
  let key = 0;

  while ((m = re.exec(s)) !== null) {
    if (m.index > last) parts.push(s.slice(last, m.index));
    parts.push(<strong key={key++}>{m[1]}</strong>);
    last = m.index + m[0].length;
  }
  if (last < s.length) parts.push(s.slice(last));
  return parts;
}
