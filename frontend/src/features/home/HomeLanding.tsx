'use client';

import {motion} from 'framer-motion';
import {
  FiActivity,
  FiArrowLeft,
  FiArrowRight,
  FiCalendar,
  FiCheck,
  FiClipboard,
  FiClock,
  FiHeart,
  FiShield,
  FiUser,
  FiUsers
} from 'react-icons/fi';
import {useLocale, useTranslations} from 'next-intl';
import {Link} from '@/i18n/navigation';
import {Footer} from '@/components/layout/Footer';
import {Header} from '@/components/layout/Header';
import {fadeIn, scaleIn, slideUp, staggerContainer} from '@/lib/animations';

const featureIcons = [FiClipboard, FiCalendar, FiActivity, FiUsers] as const;
const workflowIcons = [FiUsers, FiClipboard, FiCalendar] as const;

function MedicalIllustration() {
  return (
    <div className="relative aspect-[1.25] w-full max-w-[540px]">
      <motion.div className="absolute left-[8%] right-[8%] top-[14%] h-[68%] rounded-[30px] bg-[#17345d] shadow-[20px_20px_0_#102846] rotate-[7deg]" animate={{rotate: [7, 9, 7], y: [0, -5, 0]}} transition={{duration: 7, repeat: Infinity, ease: 'easeInOut'}} />
      <motion.div className="absolute left-[13%] top-[8%] z-10 h-[66%] w-[62%] rounded-[24px] bg-white p-[5%] shadow-2xl" animate={{y: [0, -4, 0]}} transition={{duration: 5, repeat: Infinity, ease: 'easeInOut'}}>
        <div className="flex items-center gap-3">
          <motion.span className="grid size-10 place-items-center rounded-full bg-[#d2f3ef] text-[#118d85]" animate={{scale: [1, 1.08, 1]}} transition={{duration: 2.8, repeat: Infinity}}><FiUser className="size-6 fill-current" /></motion.span>
          <span className="flex flex-1 flex-col gap-2"><i className="h-2 w-3/4 rounded-full bg-[#071b35]" /><i className="h-2 w-1/2 rounded-full bg-[#c4d2e6]" /></span>
        </div>
        <motion.div className="mt-5 h-7 rounded-lg bg-[#fde1e1] p-2" animate={{opacity: [0.7, 1, 0.7]}} transition={{duration: 2.2, repeat: Infinity}}><i className="block h-3 w-3 rounded-full bg-[#e94d58] shadow-[20px_0_0_#ed6f76,40px_0_0_#ed6f76]" /></motion.div>
        <div className="mt-4 grid grid-cols-3 gap-2">
          {[1, 2, 3].map((item) => <motion.span key={item} className="flex h-14 flex-col justify-center gap-2 rounded-xl bg-[#eff4fa] p-3" animate={{y: [0, item === 2 ? -3 : 0, 0]}} transition={{duration: 3, delay: item * .2, repeat: Infinity}}><i className="h-1.5 w-1/2 rounded-full bg-[#c1cee1]" /><i className="h-3 w-3/4 rounded-full bg-[#071b35]" /></motion.span>)}
        </div>
        <div className="mt-4 space-y-2"><i className="block h-2 w-full rounded-full bg-[#dce4ef]" /><i className="block h-2 w-3/4 rounded-full bg-[#dce4ef]" /></div>
      </motion.div>
      <motion.div className="absolute bottom-[18%] right-[8%] z-20 h-[25%] w-[36%] rounded-[20px] bg-[#2dd4bf] p-[5%] shadow-xl" animate={{y: [0, -12, 0], rotate: [0, 2, 0]}} transition={{duration: 4.5, repeat: Infinity, ease: 'easeInOut'}}>
        <i className="block h-2 w-1/2 rounded-full bg-[#062039]" /><i className="mt-3 block h-2 w-full rounded-full bg-[#168e8c]" />
        <span className="mt-3 flex gap-2"><motion.i className="size-2.5 rounded-full bg-[#075b70]" animate={{scale: [1, 1.5, 1]}} transition={{duration: 1.6, repeat: Infinity}} /><i className="size-2.5 rounded-full bg-[#168e8c]" /><i className="size-2.5 rounded-full bg-[#168e8c]" /></span>
      </motion.div>
      <svg className="absolute bottom-[5%] left-0 z-0 h-[25%] w-full overflow-visible text-brand" viewBox="0 0 500 100" fill="none" aria-hidden>
        <motion.path d="M0 65H105L125 18L155 92L180 35L200 65H500" stroke="currentColor" strokeWidth="4" strokeDasharray="520" animate={{strokeDashoffset: [520, 0, -520]}} transition={{duration: 3.2, repeat: Infinity, ease: 'linear'}} />
      </svg>
    </div>
  );
}

export function HomeLanding() {
  const t = useTranslations('Home');
  const isArabic = useLocale() === 'ar';
  const Arrow = isArabic ? FiArrowLeft : FiArrowRight;
  const tickerItems = [t('ticker.one'), t('ticker.two'), t('ticker.three'), t('ticker.four'), t('ticker.five'), t('ticker.six')];

  return (
    <div className="min-h-screen overflow-hidden bg-background">
      <Header />
      <main>
        <section className="relative mx-auto grid min-h-[620px] w-[min(1180px,calc(100%-40px))] grid-cols-1 items-center gap-12 py-20 lg:grid-cols-[1fr_1fr]">
          <div className="pointer-events-none absolute -right-40 -top-32 size-[360px] rounded-full bg-[#10b7a5] opacity-20 blur-[90px]" />
          <motion.div className="relative z-10 max-w-[600px]" initial="hidden" animate="visible" variants={staggerContainer}>
            <motion.span className="inline-flex items-center gap-2 text-xs font-extrabold text-brand" variants={slideUp}><i className="size-2 rounded-full bg-brand shadow-[0_0_0_5px_#2dd4bf1f]" />{t('badge')}</motion.span>
            <motion.h1 className="my-5 max-w-[620px] text-4xl font-black leading-[1.15] tracking-[-.04em] sm:text-6xl" variants={slideUp}>{t('title')}</motion.h1>
            <motion.p className="max-w-[550px] text-sm leading-8 text-muted sm:text-base" variants={slideUp}>{t('description')}</motion.p>
            <motion.div className="mt-7 flex flex-wrap gap-3" variants={slideUp}>
              <Link href="/#contact" className="inline-flex min-h-12 items-center justify-center gap-2 rounded-xl bg-brand px-6 text-xs font-extrabold text-[#06211f] shadow-[0_12px_30px_#2dd4bf2d] transition hover:-translate-y-1 hover:bg-[#78f3dc]">{t('primaryCta')} <Arrow className="size-4" /></Link>
              <Link href="/#howItWorks" className="inline-flex min-h-12 items-center justify-center rounded-xl border border-border px-6 text-xs font-extrabold transition hover:-translate-y-1 hover:border-brand hover:text-brand">{t('secondaryCta')}</Link>
            </motion.div>
            <motion.div className="mt-6 flex items-center gap-2.5 text-[11px] text-muted" variants={fadeIn}><span className="flex" dir="ltr"><i className="grid size-6 place-items-center rounded-full border-2 border-background bg-[#f1c7a6] text-[10px] not-italic text-[#07111f]">د</i><i className="-ml-1 grid size-6 place-items-center rounded-full border-2 border-background bg-[#b4e6df] text-[10px] not-italic text-[#07111f]">ط</i><i className="-ml-1 grid size-6 place-items-center rounded-full border-2 border-background bg-[#b9d1ef] text-[10px] not-italic text-[#07111f]">م</i></span>{t('proof')}</motion.div>
          </motion.div>
          <motion.div className="relative z-10 flex justify-center lg:justify-end" initial="hidden" animate="visible" variants={scaleIn}><MedicalIllustration /></motion.div>
        </section>

        <div className="overflow-hidden border-y border-border bg-surface" aria-label={t('tickerLabel')}>
          <motion.div className="flex w-max" animate={{x: isArabic ? ['0%', '50%'] : ['0%', '-50%']}} transition={{duration: 30, repeat: Infinity, ease: 'linear'}}>
            {[...tickerItems, ...tickerItems].map((item, index) => <span className="inline-flex items-center gap-2 whitespace-nowrap px-7 py-3.5 text-[11px] text-muted" key={`${item}-${index}`}><FiCheck className="size-3.5 text-brand" />{item}</span>)}
          </motion.div>
        </div>

        <section className="mx-auto w-[min(1180px,calc(100%-40px))] py-24" id="features">
          <motion.div className="mx-auto mb-12 max-w-[610px] text-center" initial="hidden" whileInView="visible" viewport={{once: true, amount: .3}} variants={slideUp}><span className="text-xs font-extrabold text-brand-strong">{t('featuresEyebrow')}</span><h2 className="my-3 text-3xl font-black leading-tight tracking-[-.03em] sm:text-4xl">{t('featuresTitle')}</h2><p className="text-sm leading-8 text-muted">{t('featuresDescription')}</p></motion.div>
          <motion.div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4" initial="hidden" whileInView="visible" viewport={{once: true, amount: .15}} variants={staggerContainer}>{featureIcons.map((Icon, index) => <motion.article className="min-h-[275px] rounded-2xl border border-border bg-surface p-6 transition hover:-translate-y-2 hover:border-brand hover:shadow-2xl" key={index} variants={slideUp}><span className="grid size-11 place-items-center rounded-xl bg-brand-soft text-brand-strong"><Icon className="size-5" /></span><h3 className="mt-5 text-base font-bold">{t(`featuresList.${index}.title`)}</h3><p className="mt-2 min-h-16 text-xs leading-7 text-muted">{t(`featuresList.${index}.description`)}</p><Link className="mt-3 inline-flex items-center gap-1 text-[11px] font-extrabold text-brand-strong" href="/#contact">{t('learnMore')} <Arrow className="size-3" /></Link></motion.article>)}</motion.div>
        </section>

        <section className="border-y border-border bg-surface/60">
          <div className="mx-auto grid w-[min(1180px,calc(100%-40px))] grid-cols-2 divide-x divide-border py-12 rtl:divide-x-reverse sm:grid-cols-4">
            {['patientsCount', 'visitsCount', 'timeSaved', 'availability'].map((item, index) => (
              <motion.div key={item} className="px-4 text-center first:ps-0 last:pe-0" initial={{opacity: 0, y: 12}} whileInView={{opacity: 1, y: 0}} viewport={{once: true}} transition={{delay: index * .08}}>
                <strong className="block text-2xl font-black text-brand sm:text-3xl">{t(`stats.${item}.value`)}</strong>
                <span className="mt-1 block text-[11px] text-muted">{t(`stats.${item}.label`)}</span>
              </motion.div>
            ))}
          </div>
        </section>

        <section className="mx-auto grid w-[min(1180px,calc(100%-40px))] items-center gap-10 py-24 lg:grid-cols-[.8fr_1.2fr] lg:gap-20" id="howItWorks">
          <div className="max-w-[610px]"><span className="text-xs font-extrabold text-brand-strong">{t('workflowEyebrow')}</span><h2 className="my-3 text-3xl font-black leading-tight tracking-[-.03em] sm:text-4xl">{t('workflowTitle')}</h2><p className="text-sm leading-8 text-muted">{t('workflowDescription')}</p></div>
          <div className="grid gap-3.5">{workflowIcons.map((Icon, index) => <motion.div className="grid grid-cols-[38px_48px_1fr] items-center gap-4 rounded-2xl border border-border bg-surface p-4" key={index} initial="hidden" whileInView="visible" viewport={{once: true}} variants={slideUp}><span className="text-xs font-black text-brand">0{index + 1}</span><span className="grid size-12 place-items-center rounded-xl bg-brand-soft text-brand-strong"><Icon /></span><div><h3 className="m-0 text-sm font-bold">{t(`workflow.${index}.title`)}</h3><p className="m-0 mt-1 text-xs leading-6 text-muted">{t(`workflow.${index}.description`)}</p></div></motion.div>)}</div>
        </section>

        <section className="relative mx-auto mb-24 grid w-[min(1180px,calc(100%-40px))] items-center gap-9 overflow-hidden rounded-3xl bg-[#0b1c2f] px-7 py-12 text-[#f2f7fb] sm:px-20 lg:grid-cols-[1fr_300px]"><div className="max-w-[590px]"><span className="text-xs font-extrabold text-brand">{t('securityEyebrow')}</span><h2 className="my-3 text-3xl font-black leading-tight sm:text-4xl">{t('securityTitle')}</h2><p className="text-sm leading-8 text-[#9aafbf]">{t('securityDescription')}</p><ul className="mt-6 grid gap-3 p-0 text-xs text-[#c8d6df]"><li className="flex items-center gap-2"><FiShield className="text-brand" />{t('securityPoints.0')}</li><li className="flex items-center gap-2"><FiHeart className="text-brand" />{t('securityPoints.1')}</li><li className="flex items-center gap-2"><FiClock className="text-brand" />{t('securityPoints.2')}</li></ul></div><div className="relative grid h-[230px] place-items-center rounded-[22px] border border-white/[.07] bg-[#10283d] text-brand"><FiShield className="relative z-10 size-11" /><span className="absolute size-[135px] rounded-full border border-brand/25" /><span className="absolute size-[190px] rounded-full border border-dashed border-brand/25" /><strong className="absolute mt-[120px] text-3xl text-[#f3f8fb]">100%</strong><small className="absolute mt-[165px] text-[10px] text-[#9aafbf]">{t('protected')}</small></div></section>

        <section className="mx-auto mb-24 w-[min(1180px,calc(100%-40px))]">
          <div className="mx-auto mb-10 max-w-[600px] text-center"><span className="text-xs font-extrabold text-brand-strong">{t('storyEyebrow')}</span><h2 className="my-3 text-3xl font-black sm:text-4xl">{t('storyTitle')}</h2><p className="text-sm leading-8 text-muted">{t('storyDescription')}</p></div>
          <div className="grid gap-4 md:grid-cols-3">
            {[0, 1, 2].map((index) => <motion.article key={index} className="rounded-2xl border border-border bg-surface p-6" initial={{opacity: 0, y: 20}} whileInView={{opacity: 1, y: 0}} viewport={{once: true}} transition={{delay: index * .1}}><div className="mb-5 flex gap-1 text-brand">{[1, 2, 3, 4, 5].map((star) => <span key={star}>★</span>)}</div><p className="min-h-24 text-sm leading-7 text-muted">“{t(`testimonials.${index}.quote`)}”</p><div className="mt-5 border-t border-border pt-4"><strong className="block text-sm">{t(`testimonials.${index}.name`)}</strong><span className="text-[11px] text-muted">{t(`testimonials.${index}.role`)}</span></div></motion.article>)}
          </div>
        </section>

        <section className="mx-auto mb-24 w-[min(920px,calc(100%-40px))] rounded-3xl border border-brand/20 bg-gradient-to-br from-[#123f48] to-[#0d2539] px-5 py-14 text-center"><span className="text-xs font-extrabold text-brand">{t('ctaEyebrow')}</span><h2 className="my-3 text-3xl font-black sm:text-4xl">{t('ctaTitle')}</h2><p className="mx-auto mb-6 max-w-[520px] text-sm leading-8 text-muted">{t('ctaDescription')}</p><Link href="/#contact" className="inline-flex min-h-12 items-center justify-center gap-2 rounded-xl bg-brand px-6 text-xs font-extrabold text-[#06211f] transition hover:-translate-y-1 hover:bg-[#78f3dc]">{t('primaryCta')} <Arrow className="size-4" /></Link></section>
      </main>
      <Footer />
    </div>
  );
}
