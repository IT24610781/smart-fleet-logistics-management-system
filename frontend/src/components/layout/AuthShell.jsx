export default function AuthShell({ children }) {
  return (
    <div className="auth-glass-page min-h-screen flex items-center justify-center bg-surface p-4 sm:p-8">
      <div className="auth-glass-card w-full max-w-5xl grid grid-cols-1 lg:grid-cols-12 rounded-xl shadow-xl overflow-hidden bg-surface-container-lowest">
        {/* Left: brand panel */}
        <div className="auth-brand-panel lg:col-span-6 bg-primary p-8 sm:p-12 flex flex-col justify-between relative overflow-hidden text-on-primary">
          <div className="absolute -right-24 -top-24 w-96 h-96 rounded-full bg-secondary/15 blur-3xl pointer-events-none" />
          <div className="absolute -left-20 -bottom-20 w-80 h-80 rounded-full bg-tertiary-container/30 blur-2xl pointer-events-none" />

          <div className="relative z-10 space-y-7">
            <div className="flex flex-col items-center gap-3.5 text-center">
              <img src="/fleet-mark.svg" alt="" className="w-[124px] h-[124px] rounded-[26px] shadow-lg" />
              <div className="flex flex-col items-center gap-1">
                <span className="font-headline-sm text-[34px] leading-[40px] font-extrabold tracking-tight text-surface-container-lowest">
                  Smart Fleet
                </span>
                <p className="font-label-md text-[10px] leading-[14px] font-semibold text-primary-fixed-dim/70 uppercase tracking-[0.12em]">
                  Fleet Logistics &amp; Operations
                </p>
              </div>
            </div>

            <div className="space-y-2 max-w-md">
              <h1 className="font-headline-sm text-[21px] leading-[27px] font-semibold tracking-tight text-surface-container-lowest/90">
                Real-time fleet telematics &amp; dynamic dispatch.
              </h1>
              <p className="font-body-sm text-[12.5px] leading-[18px] text-primary-fixed-dim/70">
                A unified platform for driver routing, mission manifests, and dispatch state.
              </p>
            </div>

            <div className="space-y-2 pt-0.5 hidden sm:block">
              {[
                { icon: 'route', title: 'Plan every route', desc: 'Create missions, assign vehicles, and keep schedules moving.' },
                { icon: 'visibility', title: 'See the whole fleet', desc: 'Track vehicle availability and trip progress from one place.' },
              ].map((f) => (
                <div key={f.title} className="flex items-center gap-2.5 py-2 px-2.5 rounded-lg bg-white/5">
                  <div className="w-[26px] h-[26px] rounded-[7px] bg-secondary/20 flex items-center justify-center text-secondary-fixed shrink-0">
                    <span className="material-symbols-outlined text-[14px]">{f.icon}</span>
                  </div>
                  <div>
                    <p className="font-title-md text-[12.5px] leading-[16px] font-semibold text-surface-container-lowest">{f.title}</p>
                    <p className="font-body-sm text-[11px] leading-[15px] text-primary-fixed-dim/70">{f.desc}</p>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="relative z-10 pt-6 mt-4 hidden sm:flex items-center justify-between">
            <div className="flex items-center gap-2">
              <span className="w-[7px] h-[7px] rounded-full bg-secondary-fixed" />
              <span className="font-body-sm text-[11px] leading-[15px] text-primary-fixed/90">One fleet. One operational view.</span>
            </div>
          </div>
        </div>

        {/* Right: form */}
        <div className="auth-glass-form lg:col-span-6 p-8 sm:p-12 flex flex-col justify-center">
          {children}
        </div>
      </div>
    </div>
  )
}