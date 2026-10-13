import { useId, useLayoutEffect, useRef } from "react";

const image = "/assets/start/planora-owl-v1.png";
const eyeBase = "/assets/start/planora-owl-eye-base-v1.png";
const eyes = [
  {
    x: 558,
    y: 493,
    rx: 78,
    ry: 79,
    rotation: -13,
    aperture:
      "M447 495C453 448 482 414 522 407C568 392 609 420 629 458C638 479 639 501 635 519C634 531 625 541 608 546C566 556 521 562 476 571C458 560 445 534 447 495Z",
  },
  {
    x: 853,
    y: 408,
    rx: 70,
    ry: 76,
    rotation: -15,
    aperture:
      "M786 382C790 341 815 316 844 315C894 307 936 344 952 383C960 404 961 430 954 446C946 460 930 466 912 470L822 486C800 467 782 432 786 382Z",
  },
];
export function gazeOffset(pointer, center, viewport) {
  const x = (pointer.x - center.x) / Math.max(1, viewport.width * 0.35);
  const y = (pointer.y - center.y) / Math.max(1, viewport.height * 0.35);
  const radius = Math.max(1, Math.hypot(x, y));
  return { x: (x / radius) * 8, y: (y / radius) * 6 };
}
export function MascotEyes() {
  const id = useId().replaceAll(":", ""),
    svg = useRef(null),
    pupils = useRef([]);
  useLayoutEffect(() => {
    const root = svg.current.closest(".getting-started"),
      media = matchMedia("(prefers-reduced-motion: reduce)");
    let frame = 0,
      touchReset;
    const current = eyes.map(() => ({ x: 0, y: 0 })),
      targets = eyes.map(() => ({ x: 0, y: 0 }));
    const paint = () =>
      pupils.current.forEach((element, index) =>
        element?.setAttribute(
          "transform",
          `translate(${current[index].x.toFixed(2)} ${current[index].y.toFixed(2)})`,
        ),
      );
    const reset = () => {
      cancelAnimationFrame(frame);
      frame = 0;
      clearTimeout(touchReset);
      current.forEach((point, index) => {
        point.x = targets[index].x = 0;
        point.y = targets[index].y = 0;
      });
      paint();
    };
    const tick = () => {
      frame = 0;
      if (
        media.matches ||
        root.dataset.motionState !== "running" ||
        document.hidden
      ) {
        reset();
        return;
      }
      let moving = false;
      current.forEach((point, index) => {
        point.x += (targets[index].x - point.x) * 0.22;
        point.y += (targets[index].y - point.y) * 0.22;
        if (
          Math.abs(point.x - targets[index].x) +
            Math.abs(point.y - targets[index].y) >
          0.05
        )
          moving = true;
      });
      paint();
      if (moving) frame = requestAnimationFrame(tick);
    };
    const point = (event) => {
      if (media.matches || root.dataset.motionState !== "running") {
        reset();
        return;
      }
      clearTimeout(touchReset);
      const rect = svg.current.getBoundingClientRect();
      eyes.forEach((eye, index) =>
        Object.assign(
          targets[index],
          gazeOffset(
            { x: event.clientX, y: event.clientY },
            {
              x: rect.left + (rect.width * eye.x) / 1254,
              y: rect.top + (rect.height * eye.y) / 1254,
            },
            { width: innerWidth, height: innerHeight },
          ),
        ),
      );
      if (!frame) frame = requestAnimationFrame(tick);
    };
    const leave = () => {
      targets.forEach((target) => {
        target.x = target.y = 0;
      });
      if (!frame) frame = requestAnimationFrame(tick);
    };
    const release = (event) => {
      if (event.pointerType === "touch") touchReset = setTimeout(leave, 900);
    };
    const observer = new MutationObserver(() => {
      if (root.dataset.motionState !== "running") reset();
    });
    observer.observe(root, {
      attributes: true,
      attributeFilter: ["data-motion-state"],
    });
    root.addEventListener("pointermove", point);
    root.addEventListener("pointerdown", point);
    root.addEventListener("pointerup", release);
    root.addEventListener("pointerleave", leave);
    window.addEventListener("blur", reset);
    document.addEventListener("visibilitychange", reset);
    media.addEventListener("change", reset);
    return () => {
      reset();
      observer.disconnect();
      root.removeEventListener("pointermove", point);
      root.removeEventListener("pointerdown", point);
      root.removeEventListener("pointerup", release);
      root.removeEventListener("pointerleave", leave);
      window.removeEventListener("blur", reset);
      document.removeEventListener("visibilitychange", reset);
      media.removeEventListener("change", reset);
    };
  }, []);
  return (
    <svg
      ref={svg}
      className="start-mascot-eyes"
      viewBox="0 0 1254 1254"
      aria-hidden="true"
      focusable="false"
    >
      <defs>
        <filter
          id={`${id}-feather`}
          x="-25%"
          y="-25%"
          width="150%"
          height="150%"
        >
          <feGaussianBlur stdDeviation="0.7" />
        </filter>
        {eyes.map((eye, index) => (
          <mask
            key={index}
            id={`${id}-eye-${index}`}
            maskUnits="userSpaceOnUse"
            x={eye.x - eye.rx - 15}
            y={eye.y - eye.ry - 15}
            width={eye.rx * 2 + 30}
            height={eye.ry * 2 + 30}
          >
            <ellipse
              cx={eye.x}
              cy={eye.y}
              rx={eye.rx}
              ry={eye.ry}
              transform={`rotate(${eye.rotation} ${eye.x} ${eye.y})`}
              fill="white"
              filter={`url(#${id}-feather)`}
            />
          </mask>
        ))}
        {eyes.map((eye, index) => (
          <clipPath key={index} id={`${id}-aperture-${index}`}>
            <path d={eye.aperture} />
          </clipPath>
        ))}
      </defs>
      {eyes.map((eye, index) => (
        <g key={index} clipPath={`url(#${id}-aperture-${index})`}>
          <image href={eyeBase} width="1254" height="1254" />
          <g
            className="start-eye-pupil"
            ref={(element) => {
              pupils.current[index] = element;
            }}
            transform="translate(0 0)"
          >
            <image
              href={image}
              width="1254"
              height="1254"
              mask={`url(#${id}-eye-${index})`}
            />
          </g>
        </g>
      ))}
    </svg>
  );
}
