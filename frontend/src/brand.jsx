export const brand = (
  href = "/",
  extra = "",
  label = "PLANORA — Plan smarter. Work realistically.",
) => (
  <a className={`brand brand-lockup ${extra}`} href={href} aria-label={label}>
    <img
      className="brand-image"
      src="/assets/planora-brand.png"
      width="2158"
      height="729"
      alt="PLANORA — Plan smarter. Work realistically."
      decoding="async"
    />
  </a>
);
