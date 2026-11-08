import type {Variants} from 'framer-motion';

export const fadeIn: Variants = {
  hidden: {opacity: 0},
  visible: {opacity: 1, transition: {duration: 0.4, ease: 'easeOut'}}
};

export const slideUp: Variants = {
  hidden: {opacity: 0, y: 16},
  visible: {opacity: 1, y: 0, transition: {duration: 0.45, ease: 'easeOut'}}
};

export const scaleIn: Variants = {
  hidden: {opacity: 0, scale: 0.97},
  visible: {opacity: 1, scale: 1, transition: {duration: 0.3, ease: 'easeOut'}}
};

export const staggerContainer: Variants = {
  hidden: {},
  visible: {
    transition: {
      staggerChildren: 0.08,
      delayChildren: 0.1
    }
  }
};

export const transitions = {
  fast: {duration: 0.2, ease: 'easeOut'},
  base: {duration: 0.35, ease: 'easeOut'},
  slow: {duration: 0.6, ease: 'easeOut'}
} as const;
