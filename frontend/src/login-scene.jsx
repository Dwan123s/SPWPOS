const spriteRoot = "/assets/login-sprites";
const spriteFrames = {
  calendar: ["calendar", "calendar-frame-02", "calendar-frame-03"],
  checklist: ["checklist", "checklist-frame-02", "checklist-frame-03"],
};

function RibbonShine({ id }) {
  return (
    <svg
      className="login-ribbon-shine"
      viewBox="0 0 1000 1000"
      aria-hidden="true"
      focusable="false"
    >
      <defs>
        <filter id={`${id}-rim`} colorInterpolationFilters="sRGB">
          <feMorphology
            in="SourceAlpha"
            operator="erode"
            radius="4"
            result="inset"
          />
          <feComposite
            in="SourceAlpha"
            in2="inset"
            operator="out"
            result="edge"
          />
          <feFlood floodColor="white" />
          <feComposite in2="edge" operator="in" />
        </filter>
        <mask
          id={`${id}-mask`}
          maskUnits="userSpaceOnUse"
          x="0"
          y="0"
          width="1000"
          height="1000"
        >
          <image
            href={`${spriteRoot}/ribbon.webp`}
            width="1000"
            height="1000"
            preserveAspectRatio="none"
            filter={`url(#${id}-rim)`}
          />
        </mask>
        <linearGradient id={`${id}-beam`} x1="0%" y1="0%" x2="100%" y2="35%">
          <stop offset="42%" stopColor="#64e6ff" stopOpacity="0" />
          <stop offset="47%" stopColor="#64e6ff" stopOpacity="0.2" />
          <stop offset="49%" stopColor="#d9faff" stopOpacity="0.9" />
          <stop offset="50%" stopColor="#ffffff" />
          <stop offset="51%" stopColor="#d9faff" stopOpacity="0.9" />
          <stop offset="53%" stopColor="#64e6ff" stopOpacity="0.2" />
          <stop offset="58%" stopColor="#64e6ff" stopOpacity="0" />
        </linearGradient>
      </defs>
      <g mask={`url(#${id}-mask)`}>
        <rect
          className="login-ribbon-shine-band"
          x="-500"
          y="-500"
          width="2000"
          height="2000"
          fill={`url(#${id}-beam)`}
        />
      </g>
    </svg>
  );
}

function Sprite({ name, className = name, depth }) {
  return (
    <div
      className={`login-sprite login-sprite--${className}`}
      data-sprite={name}
      data-depth={depth}
    >
      {(spriteFrames[name] ?? [name]).map((frame, index) => (
        <picture key={frame} className="login-sprite-frame" data-frame={index}>
          <source srcSet={`${spriteRoot}/${frame}.webp`} type="image/webp" />
          <img src={`${spriteRoot}/${frame}.png`} alt="" decoding="async" />
        </picture>
      ))}
      {name === "ribbon" && <RibbonShine id={`login-${className}-shine`} />}
    </div>
  );
}

export function LoginBackground() {
  return (
    <>
      <div className="login-background-scene" aria-hidden="true">
        <div className="login-scene-halo" />
      </div>
      <div className="login-background-ribbons" aria-hidden="true">
        <Sprite name="ribbon-background" />
      </div>
      <div className="login-motion-orbs" aria-hidden="true">
        <svg
          className="login-orb-filters"
          width="0"
          height="0"
          focusable="false"
        >
          <defs>
            <filter
              id="login-orb-directional-blur"
              x="-50%"
              y="-50%"
              width="200%"
              height="200%"
              colorInterpolationFilters="sRGB"
            >
              <feGaussianBlur stdDeviation="14 1.5" />
            </filter>
          </defs>
        </svg>
        {["upper-left", "upper-right", "lower-right"].map((position) => (
          <Sprite
            key={position}
            name="bubble"
            className={`orb-motion-${position}`}
          />
        ))}
      </div>
    </>
  );
}

export function LoginScene() {
  return (
    <div className="login-scene">
      <div className="login-scene-shadow" aria-hidden="true" />
      <LoginArtwork />
      <div className="login-headline-panel">
        <h1>
          <span className="login-headline-line">Khám phá</span>{" "}
          <span className="login-headline-line">nhịp làm việc</span>{" "}
          <span className="login-headline-line login-headline-accent">
            phù hợp
            <span className="login-headline-ray" aria-hidden="true">
              phù hợp
            </span>
          </span>{" "}
          <span className="login-headline-line login-headline-accent">
            với bạn.
            <span className="login-headline-ray" aria-hidden="true">
              với bạn.
            </span>
          </span>
        </h1>
        <div className="login-headline-dash" aria-hidden="true" />
      </div>
    </div>
  );
}

export function LoginArtwork() {
  return (
    <div className="login-scene-art" aria-hidden="true">
      <Sprite name="ribbon" className="ribbon-back" depth={1} />
      <Sprite name="calendar" depth={2} />
      <Sprite name="checklist" depth={3} />
      <Sprite name="clock" depth={4} />
      <Sprite name="ribbon" className="ribbon-front" depth={5} />
      <Sprite name="workspace-photo" />
      {["top", "left", "right", "bottom", "lower-left"].map((position) => (
        <Sprite key={position} name="bubble" className={`bubble-${position}`} />
      ))}
    </div>
  );
}
