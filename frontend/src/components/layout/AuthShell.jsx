// Shared layout for the public auth screens (Login, Dispatcher registration):
// brand panel on the left, form on the right. One place to change so the auth
// screens can never drift apart visually again.
export default function AuthShell({ children }) {
  return (
    <div className="min-h-screen flex items-center justify-center bg-surface p-4 sm:p-8">
      <div className="w-full max-w-5xl grid grid-cols-1 lg:grid-cols-12 rounded-xl shadow-xl overflow-hidden bg-surface-container-lowest">
        {/* Left: brand panel */}
        <div className="lg:col-span-6 bg-primary p-8 sm:p-12 flex flex-col justify-between relative overflow-hidden text-on-primary">
          <div className="absolute -right-24 -top-24 w-96 h-96 rounded-full bg-secondary/15 blur-3xl pointer-events-none" />
          <div className="absolute -left-20 -bottom-20 w-80 h-80 rounded-full bg-tertiary-container/30 blur-2xl pointer-events-none" />

          <div className="relative z-10 space-y-8">
            <div className="flex items-center gap-4">
              <img src="/fleet-mark.svg" alt="" className="w-12 h-12 rounded-lg shadow-sm" />
              <div>
                <span className="font-headline-sm text-headline-sm tracking-tight text-surface-container-lowest">
                  Smart Fleet
                </span>
                <p className="font-label-md text-label-md text-primary-fixed-dim uppercase tracking-wider">
                  Fleet Logistics &amp; Operations
                </p>
              </div>
            </div>

            <div className="space-y-3 max-w-md">
              <h1 className="font-headline-xl-mobile lg:font-headline-xl text-headline-xl-mobile lg:text-headline-xl font-bold tracking-tight text-surface-container-lowest">
                Real-time fleet telematics &amp; dynamic dispatch.
              </h1>
              <p className="font-body-lg text-body-lg text-primary-fixed-dim leading-relaxed">
                A unified platform for driver routing, mission manifests, and dispatch state.
              </p>
            </div>

            <div className="space-y-3 pt-2 hidden sm:block">
              {[
                { icon: 'route', title: 'Plan every route', desc: 'Create missions, assign vehicles, and keep schedules moving.' },
                { icon: 'visibility', title: 'See the whole fleet', desc: 'Track vehicle availability and trip progress from one place.' },
              ].map((f) => (
                <div key={f.title} className="flex items-center gap-3 p-3 rounded-lg bg-white/5">
                  <div className="w-8 h-8 rounded bg-secondary/20 flex items-center justify-center text-secondary-fixed">
                    <span className="material-symbols-outlined text-lg">{f.icon}</span>
                  </div>
                  <div>
                    <p className="font-title-md text-title-md text-surface-container-lowest">{f.title}</p>
                    <p className="font-body-sm text-body-sm text-primary-fixed-dim">{f.desc}</p>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="relative z-10 pt-8 mt-4 hidden sm:flex items-center justify-between">
            <div className="flex items-center gap-2">
              <span className="w-2.5 h-2.5 rounded-full bg-secondary-fixed" />
              <span className="font-body-sm text-body-sm text-primary-fixed">One fleet. One operational view.</span>
            </div>
          </div>
        </div>

        {/* Right: form */}
        <div className="lg:col-span-6 p-8 sm:p-12 flex flex-col justify-center">
          {children}
        </div>
      </div>
    </div>
  )
}
